using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NinjaTrader.NinjaScript
{
	public enum PulseGuardUpdateStatus
	{
		Atualizado,
		AtualizacaoDisponivel,
		LocalMaisNovo,
		Incompativel,
		NaoVerificado,
		Desligado
	}

	public enum PulseGuardComponent
	{
		Indicator,
		Strategy,
		Template
	}

	public sealed class PulseGuardUpdateResult
	{
		public PulseGuardUpdateStatus Status { get; private set; }
		public string LatestVersion { get; private set; }
		public string Reason { get; private set; }
		public string ReleaseNotesUrl { get; private set; }
		public DateTime? CheckedAt { get; private set; }

		internal PulseGuardUpdateResult(PulseGuardUpdateStatus status, string latestVersion,
			string reason, string releaseNotesUrl, DateTime? checkedAt)
		{
			Status = status;
			LatestVersion = latestVersion;
			Reason = reason;
			ReleaseNotesUrl = releaseNotesUrl;
			CheckedAt = checkedAt;
		}
	}

	public sealed class PulseGuardManifest
	{
		public int Schema { get; private set; }
		public string PackageVersion { get; private set; }
		public IDictionary<string, PulseGuardManifestComponent> Components { get; private set; }
		public string ReleaseNotesUrl { get; private set; }
		public string Published { get; private set; }

		internal PulseGuardManifest(int schema, string packageVersion,
			IDictionary<string, PulseGuardManifestComponent> components,
			string releaseNotesUrl, string published)
		{
			Schema = schema;
			PackageVersion = packageVersion;
			Components = components;
			ReleaseNotesUrl = releaseNotesUrl;
			Published = published;
		}
	}

	public sealed class PulseGuardManifestComponent
	{
		public string Version { get; private set; }
		public IDictionary<string, string> MinRequired { get; private set; }

		internal PulseGuardManifestComponent(string version, IDictionary<string, string> minRequired)
		{
			Version = version;
			MinRequired = minRequired;
		}
	}

	public static class PulseGuardUpdateChecker
	{
		private const string ManifestUrl =
			"https://raw.githubusercontent.com/pulseguard-labs/PulseGuard/main/version.json";
		private const int TimeoutMilliseconds = 5000;
		private const int MaximumManifestBytes = 16384;
		private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
		private static readonly object CacheLock = new object();
		private static readonly object ClientLock = new object();
		private static PulseGuardManifest cachedManifest;
		private static DateTime cacheExpiresUtc = DateTime.MinValue;
		private static Task<PulseGuardManifest> manifestRequest;
		private static HttpClient httpClient;

		public static async Task<PulseGuardUpdateResult> CheckAsync(PulseGuardComponent component,
			string localVersion, string installedIndicatorVersion, bool enabled)
		{
			if (!enabled)
				return Result(PulseGuardUpdateStatus.Desligado, null, "Checagem desligada.", null);

			try
			{
				PulseGuardManifest manifest = await GetManifestAsync().ConfigureAwait(false);
				return CheckAgainstManifest(manifest, component, localVersion,
					installedIndicatorVersion);
			}
			catch (Exception exception)
			{
				return Result(PulseGuardUpdateStatus.NaoVerificado,
					null, exception.GetType().Name + ": " + exception.Message, null);
			}
		}

		public static PulseGuardManifest ParseManifest(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
				throw new FormatException("Manifesto vazio.");

			var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumManifestBytes };
			IDictionary<string, object> root = RequireObject(serializer.DeserializeObject(json), "raiz");
			int schema = RequireInteger(root, "schema");
			if (schema != 1)
				throw new FormatException("Schema de manifesto não suportado.");
			string packageVersion = RequireString(root, "packageVersion");
			ParseSemVer(packageVersion);

			IDictionary<string, object> componentsObject = RequireObject(
				RequireValue(root, "components"), "components");
			var components = new Dictionary<string, PulseGuardManifestComponent>(
				StringComparer.OrdinalIgnoreCase);
			string[] requiredComponents = { "indicator", "strategy", "template" };
			foreach (string name in requiredComponents)
			{
				IDictionary<string, object> componentObject = RequireObject(
					RequireValue(componentsObject, name), "components." + name);
				string version = RequireString(componentObject, "version");
				ParseSemVer(version);
				var requirements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				object minRequiredValue;
				if (componentObject.TryGetValue("minRequired", out minRequiredValue)
					&& minRequiredValue != null)
				{
					IDictionary<string, object> minRequired = RequireObject(
						minRequiredValue, "components." + name + ".minRequired");
					foreach (KeyValuePair<string, object> requirement in minRequired)
					{
						string requiredVersion = RequireStringValue(requirement.Value,
							"components." + name + ".minRequired." + requirement.Key);
						ParseSemVer(requiredVersion);
						requirements.Add(requirement.Key, requiredVersion);
					}
				}
				components.Add(name, new PulseGuardManifestComponent(version, requirements));
			}

			string releaseNotesUrl = RequireString(root, "releaseNotesUrl");
			Uri releaseUri;
			if (!Uri.TryCreate(releaseNotesUrl, UriKind.Absolute, out releaseUri)
				|| releaseUri.Scheme != Uri.UriSchemeHttps)
				throw new FormatException("releaseNotesUrl deve ser uma URL HTTPS absoluta.");
			string published = RequireString(root, "published");
			DateTime publishedDate;
			if (!DateTime.TryParseExact(published, "yyyy-MM-dd", CultureInfo.InvariantCulture,
				DateTimeStyles.None, out publishedDate))
				throw new FormatException("published deve usar o formato YYYY-MM-DD.");

			return new PulseGuardManifest(schema, packageVersion, components, releaseNotesUrl, published);
		}

		public static int CompareSemVer(string left, string right)
		{
			return ParseSemVer(left).CompareTo(ParseSemVer(right));
		}

		public static PulseGuardUpdateResult CheckAgainstManifest(PulseGuardManifest manifest,
			PulseGuardComponent component, string localVersion, string installedIndicatorVersion)
		{
			if (manifest == null)
				throw new ArgumentNullException("manifest");

			DateTime checkedAt = DateTime.Now;
			string componentKey = GetComponentKey(component);
			PulseGuardManifestComponent remoteComponent = manifest.Components[componentKey];
			if (component == PulseGuardComponent.Strategy)
			{
				string requiredIndicatorVersion;
				if (remoteComponent.MinRequired.TryGetValue("indicator", out requiredIndicatorVersion)
					&& CompareSemVer(installedIndicatorVersion, requiredIndicatorVersion) < 0)
				{
					return new PulseGuardUpdateResult(PulseGuardUpdateStatus.Incompativel,
						remoteComponent.Version,
						"MACDNG " + requiredIndicatorVersion + " ou superior é necessário.",
						manifest.ReleaseNotesUrl, checkedAt);
				}
			}

			if (string.Equals(localVersion, "dev", StringComparison.OrdinalIgnoreCase))
				return new PulseGuardUpdateResult(PulseGuardUpdateStatus.LocalMaisNovo,
					remoteComponent.Version, "Versão local de desenvolvimento.",
					manifest.ReleaseNotesUrl, checkedAt);

			int comparison;
			try
			{
				comparison = CompareSemVer(localVersion, remoteComponent.Version);
			}
			catch (FormatException)
			{
				return new PulseGuardUpdateResult(PulseGuardUpdateStatus.NaoVerificado,
					remoteComponent.Version, "Versão local não informada em SemVer.",
					manifest.ReleaseNotesUrl, checkedAt);
			}

			if (comparison == 0)
				return new PulseGuardUpdateResult(PulseGuardUpdateStatus.Atualizado,
					remoteComponent.Version, "", manifest.ReleaseNotesUrl, checkedAt);
			if (comparison < 0)
				return new PulseGuardUpdateResult(PulseGuardUpdateStatus.AtualizacaoDisponivel,
					remoteComponent.Version, "Há uma versão mais recente.",
					manifest.ReleaseNotesUrl, checkedAt);
			return new PulseGuardUpdateResult(PulseGuardUpdateStatus.LocalMaisNovo,
				remoteComponent.Version, "A versão local é mais nova que o manifesto.",
				manifest.ReleaseNotesUrl, checkedAt);
		}

		private static async Task<PulseGuardManifest> GetManifestAsync()
		{
			Task<PulseGuardManifest> request;
			TaskCompletionSource<PulseGuardManifest> starter = null;
			lock (CacheLock)
			{
				if (cachedManifest != null && DateTime.UtcNow < cacheExpiresUtc)
					return cachedManifest;

				if (manifestRequest == null)
				{
					starter = new TaskCompletionSource<PulseGuardManifest>();
					manifestRequest = starter.Task;
				}
				request = manifestRequest;
			}

			if (starter != null)
				await DownloadAndCacheManifestAsync(starter).ConfigureAwait(false);
			return await request.ConfigureAwait(false);
		}

		private static async Task DownloadAndCacheManifestAsync(
			TaskCompletionSource<PulseGuardManifest> completion)
		{
			try
			{
				string json = await DownloadManifestTextAsync().ConfigureAwait(false);
				PulseGuardManifest manifest = ParseManifest(json);
				lock (CacheLock)
				{
					cachedManifest = manifest;
					cacheExpiresUtc = DateTime.UtcNow.Add(CacheDuration);
					if (ReferenceEquals(manifestRequest, completion.Task))
						manifestRequest = null;
				}
				completion.TrySetResult(manifest);
			}
			catch (Exception exception)
			{
				lock (CacheLock)
				{
					if (ReferenceEquals(manifestRequest, completion.Task))
						manifestRequest = null;
				}
				completion.TrySetException(exception);
			}
		}

		private static async Task<string> DownloadManifestTextAsync()
		{
			HttpClient client = GetHttpClient();
			using (var cancellation = new CancellationTokenSource(TimeoutMilliseconds))
			using (var request = new HttpRequestMessage(HttpMethod.Get, ManifestUrl))
			{
				request.Headers.UserAgent.ParseAdd("PulseGuard-VersionCheck/1.0");
				request.Headers.Accept.ParseAdd("application/json");
				using (HttpResponseMessage response = await client.SendAsync(request,
					HttpCompletionOption.ResponseHeadersRead, cancellation.Token).ConfigureAwait(false))
				{
					response.EnsureSuccessStatusCode();
					if (response.Content.Headers.ContentLength.HasValue
						&& response.Content.Headers.ContentLength.Value > MaximumManifestBytes)
						throw new InvalidDataException("Manifesto excede o tamanho permitido.");

					using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
					using (var output = new MemoryStream())
					{
						byte[] buffer = new byte[2048];
						int total = 0;
						int read;
						while ((read = await stream.ReadAsync(buffer, 0, buffer.Length,
							cancellation.Token).ConfigureAwait(false)) > 0)
						{
							total += read;
							if (total > MaximumManifestBytes)
								throw new InvalidDataException("Manifesto excede o tamanho permitido.");
							output.Write(buffer, 0, read);
						}
						return Encoding.UTF8.GetString(output.ToArray());
					}
				}
			}
		}

		private static HttpClient GetHttpClient()
		{
			lock (ClientLock)
			{
				if (httpClient != null)
					return httpClient;

				var handler = new HttpClientHandler
				{
					AllowAutoRedirect = false,
					UseCookies = false,
					UseDefaultCredentials = false,
					SslProtocols = SslProtocols.Tls12
				};
				httpClient = new HttpClient(handler);
				return httpClient;
			}
		}

		private static PulseGuardUpdateResult Result(PulseGuardUpdateStatus status,
			string latestVersion, string reason, string releaseNotesUrl)
		{
			return new PulseGuardUpdateResult(status, latestVersion, reason,
				releaseNotesUrl, status == PulseGuardUpdateStatus.Desligado ? (DateTime?)null : DateTime.Now);
		}

		private static string GetComponentKey(PulseGuardComponent component)
		{
			switch (component)
			{
				case PulseGuardComponent.Indicator:
					return "indicator";
				case PulseGuardComponent.Strategy:
					return "strategy";
				case PulseGuardComponent.Template:
					return "template";
				default:
					throw new ArgumentOutOfRangeException("component");
			}
		}

		private static IDictionary<string, object> RequireObject(object value, string path)
		{
			IDictionary<string, object> result = value as IDictionary<string, object>;
			if (result == null)
				throw new FormatException(path + " deve ser um objeto JSON.");
			return result;
		}

		private static object RequireValue(IDictionary<string, object> source, string key)
		{
			object value;
			if (!source.TryGetValue(key, out value) || value == null)
				throw new FormatException("Campo obrigatório ausente: " + key + ".");
			return value;
		}

		private static string RequireString(IDictionary<string, object> source, string key)
		{
			return RequireStringValue(RequireValue(source, key), key);
		}

		private static string RequireStringValue(object value, string path)
		{
			string result = value as string;
			if (string.IsNullOrWhiteSpace(result))
				throw new FormatException(path + " deve ser texto não vazio.");
			return result;
		}

		private static int RequireInteger(IDictionary<string, object> source, string key)
		{
			object value = RequireValue(source, key);
			if (value is int)
				return (int)value;
			if (value is long && (long)value >= int.MinValue && (long)value <= int.MaxValue)
				return (int)(long)value;
			throw new FormatException(key + " deve ser um inteiro.");
		}

		private static PulseGuardSemanticVersion ParseSemVer(string value)
		{
			PulseGuardSemanticVersion parsed;
			if (!PulseGuardSemanticVersion.TryParse(value, out parsed))
				throw new FormatException("Versão SemVer inválida: " + value + ".");
			return parsed;
		}

		private sealed class PulseGuardSemanticVersion : IComparable<PulseGuardSemanticVersion>
		{
			private static readonly Regex Pattern = new Regex(
				@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
				RegexOptions.CultureInvariant);
			private readonly string major;
			private readonly string minor;
			private readonly string patch;
			private readonly string[] prerelease;

			private PulseGuardSemanticVersion(string major, string minor, string patch,
				string prerelease)
			{
				this.major = major;
				this.minor = minor;
				this.patch = patch;
				this.prerelease = prerelease == null ? null : prerelease.Split('.');
			}

			public static bool TryParse(string value, out PulseGuardSemanticVersion parsed)
			{
				parsed = null;
				if (string.IsNullOrWhiteSpace(value))
					return false;
				Match match = Pattern.Match(value);
				if (!match.Success)
					return false;

				string prereleaseValue = match.Groups[4].Success ? match.Groups[4].Value : null;
				if (prereleaseValue != null)
				{
					foreach (string identifier in prereleaseValue.Split('.'))
					{
						if (IsNumeric(identifier) && identifier.Length > 1 && identifier[0] == '0')
							return false;
					}
				}

				parsed = new PulseGuardSemanticVersion(match.Groups[1].Value,
					match.Groups[2].Value, match.Groups[3].Value, prereleaseValue);
				return true;
			}

			public int CompareTo(PulseGuardSemanticVersion other)
			{
				int result = CompareNumeric(major, other.major);
				if (result != 0)
					return result;
				result = CompareNumeric(minor, other.minor);
				if (result != 0)
					return result;
				result = CompareNumeric(patch, other.patch);
				if (result != 0)
					return result;

				if (prerelease == null)
					return other.prerelease == null ? 0 : 1;
				if (other.prerelease == null)
					return -1;

				int count = Math.Min(prerelease.Length, other.prerelease.Length);
				for (int i = 0; i < count; i++)
				{
					bool leftNumeric = IsNumeric(prerelease[i]);
					bool rightNumeric = IsNumeric(other.prerelease[i]);
					if (leftNumeric && rightNumeric)
						result = CompareNumeric(prerelease[i], other.prerelease[i]);
					else if (leftNumeric != rightNumeric)
						result = leftNumeric ? -1 : 1;
					else
						result = string.CompareOrdinal(prerelease[i], other.prerelease[i]);
					if (result != 0)
						return result;
				}
				return prerelease.Length.CompareTo(other.prerelease.Length);
			}

			private static int CompareNumeric(string left, string right)
			{
				if (left.Length != right.Length)
					return left.Length.CompareTo(right.Length);
				return string.CompareOrdinal(left, right);
			}

			private static bool IsNumeric(string value)
			{
				for (int i = 0; i < value.Length; i++)
				{
					if (value[i] < '0' || value[i] > '9')
						return false;
				}
				return value.Length > 0;
			}
		}
	}
}
