using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.WinAny.IO;
using static FileEncrptor.Encryptor;

namespace FileEncrptor
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        ///     Markup byte array
        /// </summary>
        private readonly byte[] _markupByteArray =
        {
            0x43, 0x72, 0x79, 0x70, 0x74, 0x65, 0x64, 0x20, 0x62, 0x79, 0x20, 0x46, 0x69, 0x6c, 0x65, 0x45, 0x6e, 0x63,
            0x72, 0x79, 0x70, 0x74, 0x6f, 0x72, 0x20, 0x6d, 0x61, 0x64, 0x65, 0x20, 0x62, 0x79, 0x20, 0x41, 0x73, 0x74,
            0x79, 0x72, 0x23, 0x33, 0x35, 0x33, 0x35
        };

        public MainWindow()
        {
            InitializeComponent();
        }


        /// <summary>
        ///     File management buttons logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileAddButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Create a file selector
            var openFileDlg = new OpenFileDialog
            {
                Multiselect = true, CheckFileExists = true, CheckPathExists = true, Filter = "All files (*.*)|*.*"
            };

            // Open the file selector dialog
            var result = openFileDlg.ShowDialog();

            // Check for results and add items to the list
            if (result != true) return;
            foreach (var s in openFileDlg.FileNames)
                FileList.Items.Add(s);
        }

        private void RemoveSelected_OnClick(object sender, RoutedEventArgs e)
        {
            // CHeck if there is selected items
            if (FileList.SelectedItems.Count == 0) return;

            // Delete the selected items
            for (var i = FileList.Items.Count - 1; i >= 0; i--) FileList.Items.RemoveAt(i);
        }


        /// <summary>
        ///     Password text box logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PasswordBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Dynamically enable or disable buttons
            if (FileList.Items.Count == 0 && (PasswordBox.Text == "Password" ||
                                              string.IsNullOrEmpty(PasswordBox.Text) ||
                                              string.IsNullOrWhiteSpace(PasswordBox.Text)))
            {
                EncryptButton.IsEnabled = false;
                DecryptButton.IsEnabled = false;
            }
            else
            {
                EncryptButton.IsEnabled = true;
                DecryptButton.IsEnabled = true;
            }
        }

        private void PasswordBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            // Remove placeholder text
            if (PasswordBox.Text == "Password")
                PasswordBox.Text = "";
        }

        private void PasswordBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            // Add placeholder text
            if (PasswordBox.Text == "")
                PasswordBox.Text = "Password";
        }


        /// <summary>
        ///     Remove files after operation checkbox logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RmvFileAftEncrypt(object sender, RoutedEventArgs e)
        {
            // Prevent the dialog from being showed if check is being unchecked
            if (RmvFileAftEncryptCheckbox.IsChecked == false) return;

            RmvFileAftEncryptCheckbox.IsChecked = false;

            // Warning prompt
            var messageBoxResult = MessageBox.Show(
                "Warning, files will be lost forever !\nPlease note that this apply to decryption, where encrypted files will be deleted.\n\nProceed ?",
                "Warning !", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            // If user wants to proceed, checkbox is checked
            if (messageBoxResult == MessageBoxResult.Yes)
                RmvFileAftEncryptCheckbox.IsChecked = true;
        }

        private void GuiLock(bool disable)
        {
            if (disable)
            {
                EncryptButton.IsEnabled = false;
                DecryptButton.IsEnabled = false;
                RmvFileAftEncryptCheckbox.IsEnabled = false;
                PasswordBox.IsEnabled = false;
                addFilesButton.IsEnabled = false;
                removeFilesButton.IsEnabled = false;
                FileList.IsEnabled = false;
            }
            else
            {
                EncryptButton.IsEnabled = true;
                DecryptButton.IsEnabled = true;
                RmvFileAftEncryptCheckbox.IsEnabled = true;
                PasswordBox.IsEnabled = true;
                addFilesButton.IsEnabled = true;
                removeFilesButton.IsEnabled = true;
                FileList.IsEnabled = true;
            }
        }


        /// <summary>
        /// Custom method to append bytes to a file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bytes"></param>
        private static void AppendAllBytes(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private bool FindMarkupByte(string filePath)
        {
            var s = ""; // s variable to compare markup bytes and result

            // Method to read the last 43 bytes
            using (var reader = new StreamReader(filePath))
            {
                if (reader.BaseStream.Length > 43) reader.BaseStream.Seek(-43, SeekOrigin.End);
                string line;
                while ((line = reader.ReadLine()) != null) s = line;
            }

            // Check if found bytes correspond to the markup bytes
            if (s == "Crypted by FileEncryptor made by Astyr#3535")
                return true;

            return false; // Return false if not
        }

        /// <summary>
        ///     Encrypt button logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [SuppressMessage("ReSharper.DPA", "DPA0003: Excessive memory allocations in LOH",
            MessageId = "type: System.Byte[]")]
        private async void EncryptButton_OnClick(object sender, RoutedEventArgs e)
        {
            //This bool is a fail-safe variable to prevent files from being deleted if an error occurs
            var removeFiles = true;

            //Confirmation dialog
            var messageBoxResult = MessageBox.Show(
                "You are about to encrypt files !\nPlease note that passwords cannot be recovered in anyway.\n\nProceed ?",
                "Warning !",
                MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            // Check if user wants to proceed and if password isn't null 
            if (messageBoxResult == MessageBoxResult.Yes && PasswordBox.Text != "")
            {
                // List through all the items in the list
                foreach (var v in FileList.Items)
                {
                    var s = v.ToString();

                    // Check if new files already exists
                    MessageBoxResult messageBoxResult2 = MessageBoxResult.Yes;
                    if (File.Exists(v.ToString()))
                        messageBoxResult2 =
                            MessageBox.Show(v + " \nalready exists, do you really want to replace it ?",
                                "Warning !", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (messageBoxResult2 == MessageBoxResult.No)
                    {
                        removeFiles = false;
                        break;
                    }

                    // Check for the extension to not be "aes"
                    if (s.Substring(s.Length - 3) != "aes")
                        try
                        {
                            AppendAllBytes(v.ToString(), _markupByteArray);
                            var pw = PasswordBox.Text;
                            GuiLock(true);
                            await Task.Run(() => FileEncrypt(v.ToString(), pw));
                        }
                        catch (Exception exception)
                        {
                            removeFiles = false;
                            MessageBox.Show(exception.ToString());
                        }

                    // If selected, delete the file after encryption using a secure algorithm
                    if (RmvFileAftEncryptCheckbox.IsChecked == true && removeFiles)
                    {
                        var fi1 = new FileInfo(v.ToString());
                        fi1.Delete(OverwriteAlgorithm.Random);
                    }
                }

                // If the operation has been successful, then show final message
                if (removeFiles)
                {
                    FileList.Items.Clear();
                    MessageBox.Show("Encryption done !", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    PasswordBox.Text = "";
                    GuiLock(false);
                }
            }
        }


        /// <summary>
        ///     Decrypt button logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DecryptButton_OnClick(object sender, RoutedEventArgs e)
        {
            //This bool is a fail-safe variable to prevent files from being deleted if an error occurs
            var removeFiles = true;

            // Confirmation dialog
            var messageBoxResult = MessageBox.Show(
                "You are about to decrypt files !\n\nProceed ?",
                "Warning !",
                MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            try
            {
                // Check if the user wants to proceed
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    // List through all of the items
                    foreach (var v in FileList.Items)
                    {
                        var s = v.ToString();

                        // Check for extension to be "aes"
                        if (s.Substring(s.Length - 3) == "aes")
                        {
                            // New file path creation
                            var newFile = v.ToString().Replace(".aes", "");

                            // Check if new file already exists
                            MessageBoxResult messageBoxResult2 = MessageBoxResult.Yes;
                            if (File.Exists(newFile))
                                messageBoxResult2 =
                                    MessageBox.Show(v + " \nalready exists, do you really want to replace it ?",
                                        "Warning !", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                            if (messageBoxResult2 == MessageBoxResult.No)
                            {
                                removeFiles = false;
                                break;
                            }

                            // Put password in another var
                            var pw = PasswordBox.Text;

                            // Lock the GUI
                            GuiLock(true);

                            // Decryption in another thread
                            Task.Run(() => FileDecrypt(v.ToString(), newFile, pw));

                            // Check if file has been successfully decrypted by reading the markup byte
                            if (!FindMarkupByte(newFile))
                            {
                                // Show an error message and prevent the files from being deleted if an error occurs
                                MessageBox.Show("Oops, an error as occured, maybe a wrong password ?", "Error !",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                var fi2 = new FileInfo(newFile);
                                fi2.Delete(OverwriteAlgorithm.Quick);
                                removeFiles = false;
                                break;
                            }

                            // Delete markup bytes
                            var deleteMarkup = new FileInfo(newFile);
                            var fsDm = deleteMarkup.Open(FileMode.Open);

                            long bytesToDelete = 43;
                            fsDm.SetLength(Math.Max(0, deleteMarkup.Length - bytesToDelete));

                            fsDm.Close();

                            // Remove files if asked
                            if ( /*removeFiles &&*/ RmvFileAftEncryptCheckbox.IsChecked == true)
                            {
                                var fi1 = new FileInfo(v.ToString());
                                fi1.Delete(OverwriteAlgorithm.Random);
                            }
                        }
                    }

                    // Successful operation message and unlock GUI
                    if (removeFiles)
                    {
                        MessageBox.Show("Decryption done !", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                        FileList.Items.Clear();
                        PasswordBox.Text = "";
                        GuiLock(false);
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
                throw;
            }
        }
    }
}