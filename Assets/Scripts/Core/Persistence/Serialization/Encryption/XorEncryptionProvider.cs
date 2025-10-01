using System;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// XOR-based encryption provider (replace with stronger encryption for production).
  /// </summary>
  public class XorEncryptionProvider : IEncryptionProvider {
    private readonly string _key;

    public XorEncryptionProvider(string key) {
      if (string.IsNullOrEmpty(key)) {
        throw new ArgumentException("Encryption key cannot be null or empty", nameof(key));
      }
      _key = key;
    }

    public string Encrypt(string plainText) {
      char[] data = plainText.ToCharArray();
      char[] keyArray = _key.ToCharArray();

      for (int i = 0; i < data.Length; i++) {
        data[i] = (char)(data[i] ^ keyArray[i % keyArray.Length]);
      }

      return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
    }

    public string Decrypt(string encryptedText) {
      byte[] data = Convert.FromBase64String(encryptedText);
      char[] chars = System.Text.Encoding.UTF8.GetString(data).ToCharArray();
      char[] keyArray = _key.ToCharArray();

      for (int i = 0; i < chars.Length; i++) {
        chars[i] = (char)(chars[i] ^ keyArray[i % keyArray.Length]);
      }

      return new string(chars);
    }
  }
}
