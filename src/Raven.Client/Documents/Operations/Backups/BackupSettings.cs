using System;
using Raven.Client.ServerWide.Operations.Certificates;
using Sparrow.Json.Parsing;

namespace Raven.Client.Documents.Operations.Backups
{
    public abstract class BackupSettings
    {
        public bool Disabled { get; set; }

        public GetBackupConfigurationScript GetBackupConfigurationScript { get; set; }

        public virtual bool HasSettings()
        {
            return GetBackupConfigurationScript != null &&
                   string.IsNullOrWhiteSpace(GetBackupConfigurationScript.Exec) == false;
        }

        public virtual bool WasEnabled(BackupSettings other)
        {
            return Disabled && other.Disabled == false;
        }

        public virtual DynamicJsonValue ToAuditJson()
        {
            return new DynamicJsonValue
            {
                [nameof(Disabled)] = Disabled,
                [nameof(GetBackupConfigurationScript)] = GetBackupConfigurationScript?.ToAuditJson()

            };
        }

        public virtual DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(Disabled)] = Disabled,
                [nameof(GetBackupConfigurationScript)] = GetBackupConfigurationScript?.ToJson()
            };
        }
    }

    public sealed class GetBackupConfigurationScript
    {
        public GetBackupConfigurationScript()
        {
            TimeoutInMs = 10_000;
        }

        internal GetBackupConfigurationScript(GetBackupConfigurationScript script)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));

            Arguments = script.Arguments;
            Exec = script.Exec;
            TimeoutInMs = script.TimeoutInMs;
        }

        [SecurityClearance(SecurityClearance.Operator)]
        public string Exec { get; set; }

        [SecurityClearance(SecurityClearance.Operator)]
        public string Arguments { get; set; }

        public int TimeoutInMs { get; set; }

        public DynamicJsonValue ToAuditJson()
        {

            return new DynamicJsonValue
            {
                [nameof(Exec)] = Exec,
                [nameof(TimeoutInMs)] = TimeoutInMs
            };
        }

        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(Exec)] = Exec,
                [nameof(Arguments)] = Arguments,
                [nameof(TimeoutInMs)] = TimeoutInMs
            };
        }
    }

    public sealed class LocalSettings : BackupSettings
    {
        /// <summary>
        /// Path to local folder. If not empty, backups will be held in this folder and not deleted. 
        /// Otherwise, backups will be created in the TempDir of a database and deleted after successful upload to S3/Glacier/Azure.
        /// </summary>
        public string FolderPath { get; set; }

        public int? ShardNumber { get; set; }

        public override bool HasSettings()
        {
            if (base.HasSettings())
                return true;

            return string.IsNullOrWhiteSpace(FolderPath) == false;
        }

        public bool Equals(LocalSettings other)
        {
            if (other == null)
                return false;

            if (WasEnabled(other))
                return true;

            return other.FolderPath.Equals(FolderPath);
        }


        public override DynamicJsonValue ToAuditJson()
        {
            var djv = base.ToAuditJson();

            djv[nameof(FolderPath)] = FolderPath;

            return djv;
        }


        public override DynamicJsonValue ToJson()
        {
            var djv = base.ToJson();

            djv[nameof(FolderPath)] = FolderPath;

            return djv;
        }
    }

    public abstract class AmazonSettings : BackupSettings, ICloudBackupSettings
    {
        public string AwsAccessKey { get; set; }

        public string AwsSecretKey { get; set; }

        public string AwsSessionToken { get; set; }

        /// <summary>
        /// Amazon Web Services (AWS) region.
        /// </summary>
        public string AwsRegionName { get; set; }

        /// <summary>
        /// Remote folder name.
        /// </summary>
        public string RemoteFolderName { get; set; }

        public override DynamicJsonValue ToJson()
        {
            var djv = base.ToJson();

            djv[nameof(AwsAccessKey)] = AwsAccessKey;
            djv[nameof(AwsSecretKey)] = AwsSecretKey;
            djv[nameof(AwsRegionName)] = AwsRegionName;
            djv[nameof(AwsSessionToken)] = AwsSessionToken;
            djv[nameof(RemoteFolderName)] = RemoteFolderName;

            return djv;
        }

        public override DynamicJsonValue ToAuditJson()
        {
            var djv = base.ToAuditJson();

            djv[nameof(AwsRegionName)] = AwsRegionName;
            djv[nameof(RemoteFolderName)] = RemoteFolderName;

            return djv;
        }

    }

    public sealed class S3Settings : AmazonSettings
    {
        /// <summary>
        /// S3 Bucket name.
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// S3 server Url when using custom server
        /// </summary>
        public string CustomServerUrl { get; set; }

        public bool ForcePathStyle { get; set; }

        public S3Settings()
        {
        }

        internal S3Settings(S3Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            BucketName = settings.BucketName;
            CustomServerUrl = settings.CustomServerUrl;
            ForcePathStyle = settings.ForcePathStyle;
            AwsRegionName = settings.AwsRegionName;
            AwsAccessKey = settings.AwsAccessKey;
            AwsSecretKey = settings.AwsSecretKey;
            AwsSessionToken = settings.AwsSessionToken;

            RemoteFolderName = settings.RemoteFolderName;
            Disabled = settings.Disabled;

            if (settings.GetBackupConfigurationScript != null)
                GetBackupConfigurationScript = new GetBackupConfigurationScript(settings.GetBackupConfigurationScript);
        }

        public override bool HasSettings()
        {
            if (base.HasSettings())
                return true;

            return string.IsNullOrWhiteSpace(BucketName) == false;
        }

        public bool Equals(S3Settings other)
        {
            if (other == null)
                return false;

            if (WasEnabled(other))
                return true;

            if (other.AwsRegionName != AwsRegionName)
                return false;

            if (other.BucketName != BucketName)
                return false;

            if (other.RemoteFolderName != RemoteFolderName)
                return false;

            if (other.CustomServerUrl != CustomServerUrl)
                return false;

            if (other.ForcePathStyle != ForcePathStyle)
                return false;

            return true;
        }

        public override DynamicJsonValue ToJson()
        {
            var djv = base.ToJson();
            djv[nameof(BucketName)] = BucketName;
            djv[nameof(CustomServerUrl)] = CustomServerUrl;
            djv[nameof(ForcePathStyle)] = ForcePathStyle;
            return djv;
        }

        public override DynamicJsonValue ToAuditJson()
        {
            var djv = base.ToAuditJson();
            djv[nameof(BucketName)] = BucketName;
            djv[nameof(CustomServerUrl)] = CustomServerUrl;
            djv[nameof(ForcePathStyle)] = ForcePathStyle;
            return djv;
        }
    }

    public sealed class GlacierSettings : AmazonSettings
    {
        /// <summary>
        /// Amazon Glacier Vault name.
        /// </summary>
        public string VaultName { get; set; }

        public GlacierSettings()
        {

        }

        internal GlacierSettings(GlacierSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            VaultName = settings.VaultName;
            AwsRegionName = settings.AwsRegionName;
            AwsAccessKey = settings.AwsAccessKey;
            AwsSecretKey = settings.AwsSecretKey;
            AwsSessionToken = settings.AwsSessionToken;

            RemoteFolderName = settings.RemoteFolderName;
            Disabled = settings.Disabled;

            if (settings.GetBackupConfigurationScript != null)
                GetBackupConfigurationScript = new GetBackupConfigurationScript(settings.GetBackupConfigurationScript);
        }

        public override bool HasSettings()
        {
            if (base.HasSettings())
                return true;

            return string.IsNullOrWhiteSpace(VaultName) == false;
        }

        public bool Equals(GlacierSettings other)
        {
            if (other == null)
                return false;

            if (WasEnabled(other))
                return true;

            if (other.AwsRegionName != AwsRegionName)
                return false;

            if (other.VaultName != VaultName)
                return false;

            if (other.RemoteFolderName != RemoteFolderName)
                return false;

            return true;
        }

        public override DynamicJsonValue ToJson()
        {
            var djv = base.ToJson();
            djv[nameof(VaultName)] = VaultName;
            return djv;
        }
      
        public override DynamicJsonValue ToAuditJson()
        {
            var djv = base.ToAuditJson();
            djv[nameof(VaultName)] = VaultName;
            return djv;
        }
    }

    public sealed class AzureSettings : BackupSettings, ICloudBackupSettings
    {
        /// <summary>
        /// Microsoft Azure Storage Container name.
        /// </summary>
        public string StorageContainer { get; set; }

        /// <summary>
        /// Path to remote azure folder.
        /// </summary>
        public string RemoteFolderName { get; set; }

        public string AccountName { get; set; }

        public string AccountKey { get; set; }

        public string SasToken { get; set; }

        public AzureSettings()
        {
        }

        internal AzureSettings(AzureSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            AccountKey = settings.AccountKey;
            AccountName = settings.AccountName;
            RemoteFolderName = settings.RemoteFolderName;
            SasToken = settings.SasToken;
            StorageContainer = settings.StorageContainer;
            Disabled = settings.Disabled;

            if (settings.GetBackupConfigurationScript != null)
                GetBackupConfigurationScript = new GetBackupConfigurationScript(settings.GetBackupConfigurationScript);
        }

        public override bool HasSettings()
        {
            if (base.HasSettings())
                return true;

            return string.IsNullOrWhiteSpace(StorageContainer) == false;
        }

        public bool Equals(AzureSettings other)
        {
            if (other == null)
                return false;

            if (WasEnabled(other))
                return true;

            return other.RemoteFolderName == RemoteFolderName;
        }

        public override DynamicJsonValue ToJson()
        {
            var djv = base.ToJson();

            djv[nameof(StorageContainer)] = StorageContainer;
            djv[nameof(RemoteFolderName)] = RemoteFolderName;
            djv[nameof(AccountName)] = AccountName;
            djv[nameof(AccountKey)] = AccountKey;
            djv[nameof(SasToken)] = SasToken;

            return djv;
        }

        public override DynamicJsonValue ToAuditJson()
        {
            var djv = base.ToAuditJson();

            djv[nameof(StorageContainer)] = StorageContainer;
            djv[nameof(RemoteFolderName)] = RemoteFolderName;
            djv[nameof(AccountName)] = AccountName;

            return djv;
        }
    }

    public sealed class FtpSettings : BackupSettings
    {
        public string Url { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string CertificateAsBase64 { get; set; }

        public FtpSettings()
        {
        }

        internal FtpSettings(FtpSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Url = settings.Url;
            UserName = settings.UserName;
            Password = settings.Password;
            CertificateAsBase64 = settings.CertificateAsBase64;
            Disabled = settings.Disabled;

            if (settings.GetBackupConfigurationScript != null)
                GetBackupConfigurationScript = new GetBackupConfigurationScript(settings.GetBackupConfigurationScript);
        }

        public override bool HasSettings()
        {
            if (base.HasSettings())
                return true;

            return string.IsNullOrWhiteSpace(Url) == false;
        }

        public override DynamicJsonValue ToJson()
        {
            var djv = base.ToJson();

            djv[nameof(Url)] = Url;
            djv[nameof(UserName)] = UserName;
            djv[nameof(Password)] = Password;
            djv[nameof(CertificateAsBase64)] = CertificateAsBase64;

            return djv;
        }

        public override DynamicJsonValue ToAuditJson()
        {
            var djv = base.ToAuditJson();
            djv[nameof(Url)] = Url;
            djv[nameof(UserName)] = UserName;

            return djv;
        }
    }
    public sealed class GoogleCloudSettings : BackupSettings, ICloudBackupSettings
    {
        /// <summary>
        /// Google cloud storage bucket name must be globally unique
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// Path to remote bucket folder.
        /// </summary>
        public string RemoteFolderName { get; set; }

        /// <summary>
        /// Authentication credentials to your Google Cloud Storage.
        /// </summary>
        public string GoogleCredentialsJson { get; set; }

        public GoogleCloudSettings()
        {
        }

        internal GoogleCloudSettings(GoogleCloudSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            BucketName = settings.BucketName;
            RemoteFolderName = settings.RemoteFolderName;
            GoogleCredentialsJson = settings.GoogleCredentialsJson;
        }

        public override bool HasSettings()
        {
            return string.IsNullOrWhiteSpace(BucketName) == false;
        }

        public bool Equals(GoogleCloudSettings other)
        {
            if (other == null)
                return false;

            if (WasEnabled(other))
                return true;

            return other.RemoteFolderName == RemoteFolderName;
        }

        public override DynamicJsonValue ToJson()
        {
            var djv = base.ToJson();

            djv[nameof(BucketName)] = BucketName;
            djv[nameof(RemoteFolderName)] = RemoteFolderName;
            djv[nameof(GoogleCredentialsJson)] = GoogleCredentialsJson;

            return djv;
        }

        public override DynamicJsonValue ToAuditJson()
        {
            var djv = base.ToAuditJson();
            
            djv[nameof(BucketName)] = BucketName;
            djv[nameof(RemoteFolderName)] = RemoteFolderName;

            return djv;
        }
    }

    public interface ICloudBackupSettings
    {
        public string RemoteFolderName { get; set; }
    }
}
