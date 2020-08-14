using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FileEncrptor
{
    public static class Encryptor
    {
        //  Call this function to remove the key from memory after use for security
        [DllImport("KERNEL32.DLL", EntryPoint = "RtlZeroMemory")]
        public static extern bool ZeroMemory(IntPtr destination, int length);

        /// <summary>
        /// Creates a random salt that will be used to encrypt your file. This method is required on FileEncrypt.
        /// </summary>
        /// <returns></returns>

        // ReSharper disable once MemberCanBePrivate.Global
        public static byte[] GenerateRandomSalt()
        {
            var data = new byte[32];

            using (var rng = new RNGCryptoServiceProvider())
            {
                for (var i = 0; i < 10; i++)
                {
                    // Fill the buffer with the generated data
                    rng.GetBytes(data);
                }
            }

            return data;
        }

        /// <summary>
        /// Encrypts a file from its path and a plain password.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="password"></param>
        public static void FileEncrypt(string inputFile, string password)
        {
            //generate random salt
            var salt = GenerateRandomSalt();

            //create output file name
            var fsCrypt = new FileStream(inputFile + ".aes", FileMode.Create);

            //convert password string to byte array
            var passwordBytes = Encoding.UTF8.GetBytes(password);

            //Set Rijndael symmetric encryption algorithm
            // ReSharper disable once InconsistentNaming
            var AES = new RijndaelManaged {KeySize = 256, BlockSize = 128, Padding = PaddingMode.PKCS7};

            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);

            //Cipher mode
            AES.Mode = CipherMode.CFB;

            // write salt to the beginning of the output file, so in this case can be random every time
            fsCrypt.Write(salt, 0, salt.Length);

            var cs = new CryptoStream(fsCrypt, AES.CreateEncryptor(), CryptoStreamMode.Write);

            var fsIn = new FileStream(inputFile, FileMode.Open);

            //create a buffer (1mb) so only this amount will allocate in the memory and not the whole file
            var buffer = new byte[1048576];
            // ReSharper disable once TooWideLocalVariableScope
            int read;

            try
            {
                while ((read = fsIn.Read(buffer, 0, buffer.Length)) > 0)
                {
                    //Application.DoEvents(); // -> for responsive GUI, using Task will be better!
                    cs.Write(buffer, 0, read);
                }

                // Close up
                fsIn.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }
            finally
            {
                cs.Close();
                fsCrypt.Close();
            }
        }

        /// <summary>
        /// Decrypts an encrypted file with the FileEncrypt method through its path and the plain password.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="outputFile"></param>
        /// <param name="password"></param>
        public static void FileDecrypt(string inputFile, string outputFile, string password)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var salt = new byte[32];

            var fsCrypt = new FileStream(inputFile, FileMode.Open);
            fsCrypt.Read(salt, 0, salt.Length);

            // ReSharper disable once InconsistentNaming
            var AES = new RijndaelManaged {KeySize = 256, BlockSize = 128};
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Padding = PaddingMode.PKCS7;
            AES.Mode = CipherMode.CFB;

            var cs = new CryptoStream(fsCrypt, AES.CreateDecryptor(), CryptoStreamMode.Read);

            var fsOut = new FileStream(outputFile, FileMode.Create);

            // ReSharper disable once TooWideLocalVariableScope
            int read;
            var buffer = new byte[1048576];

            try
            {
                while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    //Application.DoEvents();
                    fsOut.Write(buffer, 0, read);
                }
            }
            // ReSharper disable once InconsistentNaming
            catch (CryptographicException ex_CryptographicException)
            {
                Console.WriteLine(@"CryptographicException error: " + ex_CryptographicException.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }

            try
            {
                cs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"Error by closing CryptoStream: " + ex.Message);
            }
            finally
            {
                fsOut.Close();
                fsCrypt.Close();
            }
        }
    }
}