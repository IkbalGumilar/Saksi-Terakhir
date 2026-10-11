using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaksiTerakhir.Persistence
{
    /// <summary>
    /// Authenticated encryption for portable local saves. Embedded application keys deter casual
    /// edits; a determined client owner can extract them or replay an older valid save.
    /// </summary>
    public static class ProtectedJsonSaveStore
    {
        private const int CurrentVersion = 1;
        private const int MaximumPayloadBytes = 1024 * 1024;
        private const int MaximumFileBytes = 2 * 1024 * 1024;
        private const int IvBytes = 16;
        private const int AuthenticationBytes = 32;
        private static readonly object Sync = new object();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        // Separate domains produce independent AES and HMAC keys. Keep these stable so saves
        // copied through a cloud service can be opened on another device without device secrets.
        private static readonly byte[] EncryptionKey = DeriveKey("SaksiTerakhir/ProtectedJsonSave/v1/encryption");
        private static readonly byte[] AuthenticationKey = DeriveKey("SaksiTerakhir/ProtectedJsonSave/v1/authentication");

        [Serializable]
        private sealed class Envelope
        {
            public int version;
            public string iv;
            public string ciphertext;
            public string authentication;
        }

        /// <summary>
        /// Writes a UTF-8 payload of at most 1 MiB. Existing verified data becomes the .bak file.
        /// An invalid primary is replaced without overwriting a previously verified backup.
        /// Filesystems must support File.Replace; failures are reported to the caller.
        /// </summary>
        public static void Save(string path, string payload)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is empty.", nameof(path));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (Utf8.GetByteCount(payload) > MaximumPayloadBytes)
                throw new ArgumentException("Save payload exceeds the 1 MiB limit.", nameof(payload));

            lock (Sync)
            {
                string fullPath = Path.GetFullPath(path);
                byte[] contents = Utf8.GetBytes(CreateEnvelope(payload));
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporaryPath = fullPath + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(contents, 0, contents.Length);
                        stream.Flush(true);
                    }

                    if (File.Exists(fullPath))
                    {
                        string backupPath = TryLoadInternal(fullPath, out _, out _) ? fullPath + ".bak" : null;
                        // Source and destination share a directory: replacement is atomic and
                        // avoids a delete/move gap that could lose the previous valid save.
                        File.Replace(temporaryPath, fullPath, backupPath);
                    }
                    else File.Move(temporaryPath, fullPath);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>False with an empty error means no file exists. The payload remains null on failure.</summary>
        public static bool TryLoad(string path, out string payload, out string error)
        {
            lock (Sync) return TryLoadInternal(path, out payload, out error);
        }

        /// <summary>Loads the primary, then .bak. A successful backup recovery includes a warning in error.</summary>
        public static bool TryLoadLatest(string path, out string payload, out string error)
        {
            lock (Sync)
            {
                if (TryLoadInternal(path, out payload, out string primaryError))
                {
                    error = string.Empty;
                    return true;
                }
                if (string.IsNullOrWhiteSpace(path))
                {
                    error = primaryError;
                    return false;
                }
                if (TryLoadInternal(path + ".bak", out payload, out string backupError))
                {
                    error = "Recovered the previous save from backup because the primary save was unavailable or invalid.";
                    return true;
                }
                error = string.IsNullOrEmpty(backupError) ? primaryError
                    : string.IsNullOrEmpty(primaryError) ? "Backup save: " + backupError
                    : primaryError + " Backup save: " + backupError;
                return false;
            }
        }

        private static string CreateEnvelope(string payload)
        {
            byte[] iv = new byte[IvBytes];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(iv);
            byte[] plaintext = Utf8.GetBytes(payload);
            byte[] ciphertext;
            using (Aes aes = CreateAes(iv))
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
                ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
            byte[] authentication = Authenticate(CurrentVersion, iv, ciphertext);
            return JsonUtility.ToJson(new Envelope
            {
                version = CurrentVersion,
                iv = Convert.ToBase64String(iv),
                ciphertext = Convert.ToBase64String(ciphertext),
                authentication = Convert.ToBase64String(authentication)
            }, true);
        }

        private static bool TryLoadInternal(string path, out string payload, out string error)
        {
            payload = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Save path is empty.";
                return false;
            }

            try
            {
                byte[] contents = ReadBounded(path);
                Envelope envelope = JsonUtility.FromJson<Envelope>(Utf8.GetString(contents));
                if (envelope == null || envelope.version != CurrentVersion)
                    throw new InvalidDataException("Unsupported or missing protected save version.");
                if (string.IsNullOrEmpty(envelope.iv) || string.IsNullOrEmpty(envelope.ciphertext)
                    || string.IsNullOrEmpty(envelope.authentication))
                    throw new InvalidDataException("Protected save envelope is incomplete.");

                byte[] iv = Convert.FromBase64String(envelope.iv);
                byte[] ciphertext = Convert.FromBase64String(envelope.ciphertext);
                byte[] authentication = Convert.FromBase64String(envelope.authentication);
                if (iv.Length != IvBytes || authentication.Length != AuthenticationBytes
                    || ciphertext.Length == 0 || ciphertext.Length % IvBytes != 0
                    || ciphertext.Length > MaximumPayloadBytes + IvBytes)
                    throw new InvalidDataException("Protected save field lengths are invalid.");

                // Verify first so modified data never reaches the CBC padding/decryption step.
                if (!EqualInConstantTime(authentication, Authenticate(envelope.version, iv, ciphertext)))
                    throw new InvalidDataException("Protected save authentication failed; data was changed or damaged.");

                byte[] plaintext;
                using (Aes aes = CreateAes(iv))
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
                if (plaintext.Length > MaximumPayloadBytes)
                    throw new InvalidDataException("Protected save payload exceeds the 1 MiB limit.");
                payload = Utf8.GetString(plaintext);
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException
                || exception is ArgumentException
                || exception is UnauthorizedAccessException || exception is CryptographicException
                || exception is FormatException || exception is NotSupportedException
                || exception is System.Security.SecurityException)
            {
                error = exception is InvalidDataException ? exception.Message
                    : "Save file could not be read or verified (" + exception.GetType().Name + ").";
                return false;
            }
        }

        private static byte[] ReadBounded(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaximumFileBytes)
                    throw new InvalidDataException("Protected save file is empty or exceeds the 2 MiB limit.");
                byte[] contents = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < contents.Length)
                {
                    int read = stream.Read(contents, offset, contents.Length - offset);
                    if (read == 0) throw new InvalidDataException("Protected save file is truncated.");
                    offset += read;
                }
                if (stream.ReadByte() != -1) throw new InvalidDataException("Protected save file changed while being read.");
                return contents;
            }
        }

        private static Aes CreateAes(byte[] iv)
        {
            Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = EncryptionKey;
            aes.IV = iv;
            return aes;
        }

        private static byte[] DeriveKey(string domain)
        {
            using (SHA256 hash = SHA256.Create()) return hash.ComputeHash(Utf8.GetBytes(domain));
        }

        private static byte[] Authenticate(int version, byte[] iv, byte[] ciphertext)
        {
            // Fixed-width big-endian version followed by the fixed-width IV and ciphertext.
            // No ambiguous concatenation, text encoding, or machine endianness is involved.
            byte[] authenticated = new byte[4 + iv.Length + ciphertext.Length];
            authenticated[0] = (byte)(version >> 24);
            authenticated[1] = (byte)(version >> 16);
            authenticated[2] = (byte)(version >> 8);
            authenticated[3] = (byte)version;
            Buffer.BlockCopy(iv, 0, authenticated, 4, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, authenticated, 4 + iv.Length, ciphertext.Length);
            using (var hmac = new HMACSHA256(AuthenticationKey)) return hmac.ComputeHash(authenticated);
        }

        private static bool EqualInConstantTime(byte[] left, byte[] right)
        {
            int difference = left.Length ^ right.Length;
            // Caller has already enforced the public tag length. Compare every tag byte.
            for (int i = 0; i < left.Length && i < right.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
