namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// Certificate manager class.
    /// </summary>
    public static class CertificateUtils
    {
        public static X509Certificate2 CreateCertificate(string certificateValue)
        {
            var privateKeyBase64 = GetCertificateString(
                certificateValue,
                SolutionConstants.PrivateKeyHeader,
                SolutionConstants.PrivateKeyFooter);
            var publicKeyBase64 = GetCertificateString(
                certificateValue,
                SolutionConstants.CertificateHeader,
                SolutionConstants.CertificateFooter);

            var privateKey = Convert.FromBase64String(privateKeyBase64);
            var publicKey = Convert.FromBase64String(publicKeyBase64);

            var certificate = new X509Certificate2(publicKey);

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKey, out _);

            return certificate.CopyWithPrivateKey(rsa);
        }

        /// <summary>
        /// Gets the normalized certificate string.
        /// </summary>
        /// <param name="original">Original certificate string.</param>
        /// <param name="header">Header.</param>
        /// <param name="footer">Footer.</param>
        /// <returns>The normailzed certificate string.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static string GetCertificateString(string original, string header, string footer)
        {
            GuardHelper.ArgumentNotNull(original);

            // get a substring that's everything after the startSeparator
            var afterStartSeparator = original
                .Split(new string[] { header }, StringSplitOptions.None)
                .Skip(1)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(afterStartSeparator))
            {
                throw new InvalidOperationException("Header not found in given string");
            }

            // split the remaining substring into two substrings:
            // one before and one after the end separator
            var beforeAndAfterEndSeparator =
                afterStartSeparator.Split(new string[] { footer }, StringSplitOptions.None);

            if (beforeAndAfterEndSeparator == null || beforeAndAfterEndSeparator.Length != 2)
            {
                throw new InvalidOperationException("footer not found after the start separator");
            }

            // the first substring above is what's between the separators
            var betweenSeparators = beforeAndAfterEndSeparator.FirstOrDefault();

            // trim the substring in between the header and footer
            var normalizedBody = betweenSeparators?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedBody))
            {
                throw new InvalidOperationException(
                    "there is no content between the specified header and footer");
            }

            return normalizedBody;
        }

        public static X509Certificate2 CreateTestCert()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest certificateRequest = new CertificateRequest(
                    "CN=DummyCertificate",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                X509Certificate2 certificate = certificateRequest.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddYears(1));

                return new X509Certificate2(certificate.Export(X509ContentType.Pkcs12));
            }
        }
    }
}
