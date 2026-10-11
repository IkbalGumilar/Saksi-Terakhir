using System;
using System.IO;
using NUnit.Framework;
using SaksiTerakhir.Persistence;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class ProtectedJsonSaveStoreTests
    {
        private string directory;
        private string path;

        [Serializable]
        private sealed class Envelope
        {
            public int version;
            public string iv;
            public string ciphertext;
            public string authentication;
        }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "saksi-protected-save-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "nested", "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [TestCase("")]
        [TestCase("{\"name\":\"Kian Santang\",\"note\":\"Bukti – 日本語\",\"chapter\":1}\n")]
        public void RoundTripPreservesPayloadWithoutExposingItInEnvelope(string original)
        {
            ProtectedJsonSaveStore.Save(path, original);

            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out string loaded, out string error), Is.True, error);
            Assert.That(loaded, Is.EqualTo(original));
            Assert.That(error, Is.Empty);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
            if (original.Length > 0) Assert.That(File.ReadAllText(path), Does.Not.Contain("Kian Santang"));
        }

        [Test]
        public void RepeatedSaveUsesANewIvAndCiphertextForTheSamePayload()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":4}");
            Envelope first = ReadEnvelope(path);
            ProtectedJsonSaveStore.Save(path, "{\"stage\":4}");
            Envelope second = ReadEnvelope(path);

            Assert.That(second.iv, Is.Not.EqualTo(first.iv));
            Assert.That(second.ciphertext, Is.Not.EqualTo(first.ciphertext));
            Assert.That(ProtectedJsonSaveStore.TryLoad(path + ".bak", out string backup, out string error),
                Is.True, error);
            Assert.That(backup, Is.EqualTo("{\"stage\":4}"));
        }

        [Test]
        public void CopiedSaveCanBeOpenedAtADifferentPathWithoutTheOriginalDevice()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":4}");
            string cloudCopy = Path.Combine(directory, "restored-on-another-device.json");
            File.Copy(path, cloudCopy);

            Assert.That(ProtectedJsonSaveStore.TryLoad(cloudCopy, out string payload, out string error), Is.True, error);
            Assert.That(payload, Is.EqualTo("{\"stage\":4}"));
        }

        [Test]
        public void MaximumSizePayloadRoundTripsWithinTheEnvelopeLimit()
        {
            string original = new string('x', 1024 * 1024);
            ProtectedJsonSaveStore.Save(path, original);

            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out string payload, out string error), Is.True, error);
            Assert.That(payload, Is.EqualTo(original));
        }

        [TestCase("iv")]
        [TestCase("ciphertext")]
        [TestCase("authentication")]
        [TestCase("version")]
        public void ChangingAnyAuthenticatedFieldRejectsTheSave(string field)
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":4}");
            Envelope envelope = ReadEnvelope(path);
            if (field == "version") envelope.version++;
            else if (field == "iv") envelope.iv = FlipFirstByte(envelope.iv);
            else if (field == "ciphertext") envelope.ciphertext = FlipFirstByte(envelope.ciphertext);
            else envelope.authentication = FlipFirstByte(envelope.authentication);
            File.WriteAllText(path, JsonUtility.ToJson(envelope));

            AssertRejected(path);
        }

        [Test]
        public void CiphertextCopiedFromAnotherSaveCannotUseTheOriginalAuthentication()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":1}");
            Envelope first = ReadEnvelope(path);
            ProtectedJsonSaveStore.Save(path, "{\"stage\":9}");
            Envelope second = ReadEnvelope(path);
            first.ciphertext = second.ciphertext;
            File.WriteAllText(path, JsonUtility.ToJson(first));

            AssertRejected(path);
        }

        [Test]
        public void TruncatedEnvelopeIsRejected()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":4}");
            string contents = File.ReadAllText(path);
            File.WriteAllText(path, contents.Substring(0, contents.Length / 2));

            AssertRejected(path);
        }

        [TestCase("{}")]
        [TestCase("{\"stage\":99}")]
        [TestCase("{oops")]
        [TestCase("{\"version\":1,\"iv\":\"bad base64\",\"ciphertext\":\"AA==\",\"authentication\":\"AA==\"}")]
        public void UnauthenticatedOrMalformedInputIsRejected(string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);

            AssertRejected(path);
        }

        [Test]
        public void OversizedEnvelopeIsRejectedBeforeDecoding()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var file = new FileStream(path, FileMode.Create)) file.SetLength(2 * 1024 * 1024 + 1);

            AssertRejected(path);
        }

        [Test]
        public void MissingFileIsAnOrdinaryEmptyResult()
        {
            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out string payload, out string error), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(error, Is.Empty);
            Assert.That(ProtectedJsonSaveStore.TryLoadLatest(path, out payload, out error), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(error, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LatestLoadRecoversThePreviousValidSaveWithAWarning(bool removePrimary)
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":1}");
            ProtectedJsonSaveStore.Save(path, "{\"stage\":2}");
            if (removePrimary) File.Delete(path);
            else File.WriteAllText(path, "{\"stage\":99}");

            Assert.That(ProtectedJsonSaveStore.TryLoadLatest(path, out string payload, out string warning),
                Is.True, warning);
            Assert.That(payload, Is.EqualTo("{\"stage\":1}"));
            Assert.That(warning, Does.Contain("backup"));
        }

        [Test]
        public void LatestLoadPrefersAValidPrimaryAndRotatesOnlyThePreviousSnapshot()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":1}");
            ProtectedJsonSaveStore.Save(path, "{\"stage\":2}");
            ProtectedJsonSaveStore.Save(path, "{\"stage\":3}");

            Assert.That(ProtectedJsonSaveStore.TryLoadLatest(path, out string payload, out string error),
                Is.True, error);
            Assert.That(payload, Is.EqualTo("{\"stage\":3}"));
            Assert.That(error, Is.Empty);
            Assert.That(ProtectedJsonSaveStore.TryLoad(path + ".bak", out payload, out error), Is.True, error);
            Assert.That(payload, Is.EqualTo("{\"stage\":2}"));
        }

        [Test]
        public void ReplacingACorruptPrimaryPreservesTheLastValidBackup()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":1}");
            ProtectedJsonSaveStore.Save(path, "{\"stage\":2}");
            File.WriteAllText(path, "{\"stage\":99}");
            ProtectedJsonSaveStore.Save(path, "{\"stage\":3}");

            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out string primary, out string error), Is.True, error);
            Assert.That(primary, Is.EqualTo("{\"stage\":3}"));
            Assert.That(ProtectedJsonSaveStore.TryLoad(path + ".bak", out string backup, out error), Is.True, error);
            Assert.That(backup, Is.EqualTo("{\"stage\":1}"));
        }

        [Test]
        public void LatestLoadRejectsBothFilesWhenTheBackupWasAlsoChanged()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":1}");
            ProtectedJsonSaveStore.Save(path, "{\"stage\":2}");
            File.WriteAllText(path, "broken");
            File.WriteAllText(path + ".bak", "broken backup");

            Assert.That(ProtectedJsonSaveStore.TryLoadLatest(path, out string payload, out string error), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void FailedAtomicReplacementKeepsThePrimaryAndRemovesItsTemporaryFile()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":1}");
            Directory.CreateDirectory(path + ".bak");

            Assert.Catch(() => ProtectedJsonSaveStore.Save(path, "{\"stage\":2}"));
            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out string payload, out string error), Is.True, error);
            Assert.That(payload, Is.EqualTo("{\"stage\":1}"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void OversizedPayloadCannotReplaceAnExistingSave()
        {
            ProtectedJsonSaveStore.Save(path, "{\"stage\":1}");

            Assert.Throws<ArgumentException>(() => ProtectedJsonSaveStore.Save(path, new string('x', 1024 * 1024 + 1)));
            Assert.That(ProtectedJsonSaveStore.TryLoad(path, out string payload, out string error), Is.True, error);
            Assert.That(payload, Is.EqualTo("{\"stage\":1}"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void InvalidArgumentsAreRejectedBeforeWriting()
        {
            Assert.Throws<ArgumentException>(() => ProtectedJsonSaveStore.Save(" ", "{}"));
            Assert.Throws<ArgumentNullException>(() => ProtectedJsonSaveStore.Save(path, null));
            Assert.That(ProtectedJsonSaveStore.TryLoad(" ", out string payload, out string error), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(ProtectedJsonSaveStore.TryLoadLatest(" ", out payload, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Directory.Exists(directory), Is.False);
        }

        private static Envelope ReadEnvelope(string filePath)
        {
            return JsonUtility.FromJson<Envelope>(File.ReadAllText(filePath));
        }

        private static string FlipFirstByte(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            bytes[0] ^= 1;
            return Convert.ToBase64String(bytes);
        }

        private static void AssertRejected(string filePath)
        {
            Assert.That(ProtectedJsonSaveStore.TryLoad(filePath, out string payload, out string error), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
