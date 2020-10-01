using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities.Encoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static FnSync.StreamBuffer;

namespace FnSync
{
    class ECDHManager : EncryptionManager
    {
        private readonly ECDiffieHellmanCng cng;
        private readonly byte[] publicKey;

        public ECDHManager(String phoneKeyString) : base("      ")
        {
            this.cng = new ECDiffieHellmanCng();

            cng.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            cng.HashAlgorithm = CngAlgorithm.Sha256;
            publicKey = cng.PublicKey.ToByteArray();

            byte[] phoneKey = Base64.Decode(phoneKeyString);
            CngKey k = CngKey.Import(phoneKey, CngKeyBlobFormat.EccPublicBlob);
            this.key = cng.DeriveKeyMaterial(k);
        }

        public String GetPublicKey()
        {
            return Base64.ToBase64String(publicKey);
        }
    }
}
