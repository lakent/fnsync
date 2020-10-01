using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Security.Cryptography;
using System.Text;

namespace FnSync
{
    public class EncryptionManager
    {
        private static readonly byte[] SALT = Encoding.UTF8.GetBytes("1234qweasdzxc");
        private static readonly int IV_SIZE_BYTES = 12;

        protected byte[] key;
        protected KeyParameter keyParam;

        public EncryptionManager(string code)
        {
            key = GetKey(code);
            keyParam = ParameterUtilities.CreateKeyParameter("AES", key);
        }

        private static byte[] GetKey(string code)
        {
            return new Rfc2898DeriveBytes(code, SALT, 100).GetBytes(256 / 8);
        }

        // https://github.com/luke-park/SecureCompatibleEncryptionExamples/blob/master/C%23/SCEE.cs

        private byte[] Encrypt(byte[] src)
        {
            // Generate a 96-bit nonce using a CSPRNG.
            SecureRandom rand = new SecureRandom();
            byte[] iv = new byte[IV_SIZE_BYTES];
            rand.NextBytes(iv);

            // Create the cipher instance and initialize.
            GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
            //KeyParameter keyParam = ParameterUtilities.CreateKeyParameter("AES", key);
            ParametersWithIV cipherParameters = new ParametersWithIV(keyParam, iv);
            cipher.Init(true, cipherParameters);

            // Encrypt and prepend nonce.
            byte[] ciphertextAndNonce = new byte[iv.Length + cipher.GetOutputSize(src.Length)];
            Array.Copy(iv, 0, ciphertextAndNonce, 0, iv.Length);

            int length = cipher.ProcessBytes(src, 0, src.Length, ciphertextAndNonce, iv.Length);
            cipher.DoFinal(ciphertextAndNonce, iv.Length + length);

            return ciphertextAndNonce;
        }

        public byte[] EncryptString(string src)
        {
            JObject pack = new JObject
            {
                ["data"] = src,
                ["hash"] = Sha256Hash(src),
                ["time"] = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };

            return Encrypt(Encoding.UTF8.GetBytes(pack.ToString()));
        }

        public byte[] EncryptJSON(JObject obj)
        {
            return EncryptString(obj.ToString(Newtonsoft.Json.Formatting.None));
        }

        public byte[] Decrypt(byte[] src, int start, int count)
        {
            // Retrieve the nonce and ciphertext.
            /*
            byte[] iv = new byte[IV_SIZE_BYTES];
            byte[] ciphertext = new byte[count - IV_SIZE_BYTES];
            Array.Copy(src, start, iv, 0, IV_SIZE_BYTES);
            Array.Copy(src, start + IV_SIZE_BYTES, ciphertext, 0, count - IV_SIZE_BYTES);
            */

            // Create the cipher instance and initialize.
            GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
            //KeyParameter keyParam = ParameterUtilities.CreateKeyParameter("AES", key);
            ParametersWithIV cipherParameters = new ParametersWithIV(keyParam, src, start, IV_SIZE_BYTES);
            cipher.Init(false, cipherParameters);

            // Decrypt and return result.
            int cipherTextLength = count - IV_SIZE_BYTES;
            byte[] plaintext = new byte[cipher.GetOutputSize(cipherTextLength)];
            int length = cipher.ProcessBytes(src, start + IV_SIZE_BYTES, cipherTextLength, plaintext, 0);
            cipher.DoFinal(plaintext, length);

            return plaintext;
        }

        public string DecryptToString(byte[] src, int start, int count)
        {
            return DecryptToString(src, start, count, true);
        }

        public string DecryptToString(byte[] src, int start, int count, bool checkTime)
        {
            JObject pack = JObject.Parse(Encoding.UTF8.GetString(Decrypt(src, start, count)));
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            string incomingData = pack["data"].ToString();
            string dataHash = Sha256Hash(incomingData);

            if (!dataHash.Equals(pack["hash"].ToString()))
            {
                return null;
            }

            if (checkTime)
            {
                long sendTime = (long)pack["time"];

                if (Math.Abs(now - sendTime) > 5 * 60 * 1000)
                {
                    return null;
                }
            }

            return incomingData;
        }

        public JObject DecryptToJSON(byte[] src, int start, int count)
        {
            string str = DecryptToString(src, start, count);
            return str != null ? JObject.Parse(str) : null;    
        }

        private static readonly char[] SET = "0123456789abcdef".ToCharArray();
        public static String Sha256Hash(String value)
        {
            StringBuilder Sb = new StringBuilder(64);

            using (SHA256 hash = SHA256.Create())
            {
                Encoding enc = Encoding.UTF8;
                byte[] result = hash.ComputeHash(enc.GetBytes(value));

                foreach (byte b in result)
                {
                    Sb.Append(SET[(uint)b >> 4]).Append(SET[b & 0x0F]);
                }
            }

            return Sb.ToString();
        }
    }
}


