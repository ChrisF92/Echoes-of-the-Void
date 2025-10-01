namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Defines contract for encrypting/decrypting data.
  /// </summary>
  public interface IEncryptionProvider {
    string Encrypt(string plainText);
    string Decrypt(string encryptedText);
  }
}
