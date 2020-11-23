using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhotoSorting
{
    public partial class Form1 : Form
    {
        private static string configFileName = "config.xml";
        private static Regex r = new Regex(":");
        private List<string> extensions = new List<string>();
        private List<Photo> photos = new List<Photo>();
        private int totalFiles = 0;
        private int totalChecked = 0;
        private int exifError = 0;
        private int proccessedPhotos = 0;
        private int proccessedError = 0;
        private StringBuilder log = new StringBuilder();
        private Dictionary<string, Dictionary<string, string>> destinationFolderAnalyse = new Dictionary<string, Dictionary<string, string>>();
        private int outputStructureIndex = 0;
        private int countDestinationAnalyzed = 0;
        private FileAlreadyExists fileAlreadyExistsChoice;
        private FilesAreSame filesAreSameChoice;
        private Report report = null;

        public Form1()
        {
            InitializeComponent();
            outputStructureSelect.SelectedIndex = 0;
            fileExistsSelect.SelectedIndex = 0;
            filesAreSameSelect.SelectedIndex = 0;
            LoadConfiguration(false);
        }

        private void sourceFolderButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog source = new FolderBrowserDialog()
            {
                Description = "Select the directory with the photos you want to sort.",
                ShowNewFolderButton = true,
                RootFolder = Environment.SpecialFolder.Desktop
            };

            source.SelectedPath = Directory.Exists(sourceDirectoryTextbox.Text)
                ? sourceDirectoryTextbox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (source.ShowDialog() == DialogResult.OK)
            {
                sourceDirectoryTextbox.Text = source.SelectedPath;
            }
        }

        private void destinationFolderButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog destination = new FolderBrowserDialog()
            {
                Description = "Select the directory where you want to save the sorted photos",
                ShowNewFolderButton = true,
                RootFolder = Environment.SpecialFolder.Desktop
            };

            destination.SelectedPath = Directory.Exists(targetDirectoryTextbox.Text)
                ? targetDirectoryTextbox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (destination.ShowDialog() == DialogResult.OK)
            {
                targetDirectoryTextbox.Text = destination.SelectedPath;
            }
        }

        private void analyzePhotosButton_Click(object sender, EventArgs e)
        {
            ClearForm();
            fileTypesTextbox.Enabled = false;
            sourceDirectoryTextbox.Enabled = false;
            sourceFolderIncludeSubFoldersCheckbox.Enabled = false;
            sourceFolderButton.Enabled = false;
            analyzePhotosButton.Enabled = false;

            sourceDirectoryTextbox.Text = sourceDirectoryTextbox.Text.Replace('/', '\\').TrimEnd('\\');

            if (!Directory.Exists(sourceDirectoryTextbox.Text))
            {
                MessageBox.Show("The selected source directory does not exist!", "Directory not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            analyzeBox.Visible = true;
            parseExtensions();

            Thread thread = new Thread(processAnalyzePhotos);
            thread.Start();
        }

        private void processAnalyzePhotos()
        {
            analyzeAllFiles(sourceDirectoryTextbox.Text, sourceFolderIncludeSubFoldersCheckbox.Checked);

            if (photos.Count > 0)
            {
                SetControlPropertyThreadSafe(movePhotosButton, "Enabled", true);
                SetControlPropertyThreadSafe(copyPhotosButton, "Enabled", true);
            }
        }

        private delegate void SetControlPropertyThreadSafeDelegate(Control control, string propertyName, object propertyValue);

        public static void SetControlPropertyThreadSafe(
            Control control,
            string propertyName,
            object propertyValue)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new SetControlPropertyThreadSafeDelegate
                (SetControlPropertyThreadSafe),
                new object[] { control, propertyName, propertyValue });
            }
            else
            {
                control.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.SetProperty,
                    null,
                    control,
                    new object[] { propertyValue });
            }
        }

        private string getExtension(string filename)
        {
            return Path.GetExtension(filename).ToLower().Replace(".", string.Empty);
        }

        private void analyzeAllFiles(string targetDirectory, bool subdirectories)
        {
            string[] files = Directory.GetFiles(targetDirectory);

            foreach (string file in files)
            {
                if (extensions.Contains(getExtension(file)))
                {
                    Photo p = analyzeFile(file);
                    if (p != null)
                    {
                        photos.Add(p);
                        totalChecked++;
                        SetControlPropertyThreadSafe(totalCheckedPhotosTextbox, "Text", totalChecked.ToString());
                    }
                }

                totalFiles++;
                SetControlPropertyThreadSafe(totalFoundFilesTextbox, "Text", totalFiles.ToString());
            }

            if (subdirectories)
            {
                string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
                foreach (string subdirectory in subdirectoryEntries)
                {
                    analyzeAllFiles(subdirectory, true);
                }
            }
        }

        private Photo analyzeFile(string path)
        {
            Photo p = new Photo();
            p.Path = path;

            try
            {
                var data = GetExifFromImage(path);

                p.Taken = data.Taken;
                p.Manufacturer = data.Manufacturer;
                p.Model = data.Model;
                return p;
            }
            catch
            {
                exifError++;
                SetControlPropertyThreadSafe(exifErrorTextbox, "Text", exifError.ToString());
                log.AppendLine("Exif error: " + path);
            }

            return null;
        }

        public static (string Manufacturer, string Model, DateTime Taken) GetExifFromImage(string path)
        {
            string manufacturer = "";
            string model = "";
            DateTime taken;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (Image myImage = Image.FromStream(fs, false, false))
            {
                byte[] propItemValue = myImage.GetPropertyItem(36867).Value;
                string dateTaken = r.Replace(Encoding.UTF8.GetString(propItemValue), "-", 2);
                taken = DateTime.Parse(dateTaken);

                propItemValue = myImage.GetPropertyItem(271).Value;
                manufacturer = Encoding.UTF8.GetString(propItemValue).Replace("\0", string.Empty);

                propItemValue = myImage.GetPropertyItem(272).Value;
                model = Encoding.UTF8.GetString(propItemValue).Replace("\0", string.Empty);

                return (manufacturer, model, taken);
            }
        }

        private void parseExtensions()
        {
            string extensions = String.Concat(fileTypesTextbox.Text.Where(c => !Char.IsWhiteSpace(c))).ToLower();
            String[] parsed = System.Text.RegularExpressions.Regex.Split(extensions, ",");
            this.extensions = new List<string>(parsed);
        }

        private void showReportButton_Click(object sender, EventArgs e)
        {
            if(report != null)
            {
                report.Close();
            }

            report = new Report(log.ToString());

            report.Show();
        }

        private static bool CompareHashes(string a, string b)
        {
            return a.Equals(b);
        }

        private static string GetFileName(string path)
        {
            return Path.GetFileName(path);
        }

        private void saveConigurationButton_Click(object sender, EventArgs e)
        {
            Configuration config = new Configuration();
            config.sourceDirectory = sourceDirectoryTextbox.Text;
            config.targetDirectory = targetDirectoryTextbox.Text;
            config.fileTypes = fileTypesTextbox.Text;
            config.outputStructure = outputStructureSelect.SelectedIndex;
            config.ownFormat = ownFormatTextbox.Text;
            config.fileAlreadyExists = (FileAlreadyExists)fileExistsSelect.SelectedIndex;
            config.filesAreSame = (FilesAreSame)filesAreSameSelect.SelectedIndex;
            config.subFolders = sourceFolderIncludeSubFoldersCheckbox.Checked;

            try
            {
                MemoryStream mem = new MemoryStream();
                System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(config.GetType());
                ser.Serialize(mem, config);
                ASCIIEncoding ascii = new ASCIIEncoding();
                File.WriteAllText(configFileName, ascii.GetString(mem.ToArray()));
            }
            catch
            {
                MessageBox.Show("A failure occurred while saving the configuration!", "Cannot save configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadConfigurationButton_Click(object sender, EventArgs e)
        {
            LoadConfiguration();

        }

        private void LoadConfiguration(bool prompt = true)
        {
            if (File.Exists(configFileName))
            {
                string data = File.ReadAllText(configFileName);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
                MemoryStream mem = new MemoryStream(bytes);
                System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(typeof(Configuration));
                Configuration config = (Configuration)ser.Deserialize(mem);
                sourceDirectoryTextbox.Text = config.sourceDirectory;
                targetDirectoryTextbox.Text = config.targetDirectory;
                fileTypesTextbox.Text = config.fileTypes;
                outputStructureSelect.SelectedIndex = config.outputStructure;
                ownFormatTextbox.Text = config.ownFormat;
                fileExistsSelect.SelectedIndex = (int)config.fileAlreadyExists;
                filesAreSameSelect.SelectedIndex = (int)config.filesAreSame;
                sourceFolderIncludeSubFoldersCheckbox.Checked = config.subFolders;
            }
            else if (prompt)
            {
                MessageBox.Show("Configuration file does not exists!", "Not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void clearButton_Click(object sender, EventArgs e)
        {
            ClearForm();
            fileTypesTextbox.Enabled = true;
            sourceDirectoryTextbox.Enabled = true;
            targetDirectoryTextbox.Enabled = true;
            sourceFolderIncludeSubFoldersCheckbox.Enabled = true;
            destinationFolderButton.Enabled = true;
            sourceFolderButton.Enabled = true;
            outputStructureSelect.Enabled = true;
            fileExistsSelect.Enabled = true;
            filesAreSameSelect.Enabled = true;
            analyzePhotosButton.Enabled = true;

            analyzeBox.Visible = false;
            proccessBox.Visible = false;
            copyPhotosButton.Enabled = false;
            movePhotosButton.Enabled = false;
        }

        private void ClearForm()
        {
            extensions.Clear();
            photos.Clear();
            totalFiles = 0;
            totalChecked = 0;
            exifError = 0;
            log.Clear();
            totalFoundFilesTextbox.Text = "0";
            totalCheckedPhotosTextbox.Text = "0";
            exifErrorTextbox.Text = "0";
        }

        private void copyPhotosButton_Click(object sender, EventArgs e)
        {
            actionLabel.Text = "Copied:";
            processPhotosRunner(Action.Copy);
        }

        private void movePhotosButton_Click(object sender, EventArgs e)
        {
            actionLabel.Text = "Moved:";
            processPhotosRunner(Action.Move);
        }

        void processPhotosRunner(Action action)
        {
            destinationFolderAnalyse.Clear();
            proccessedPhotos = 0;
            proccessedError = 0;
            countDestinationAnalyzed = 0;
            proccessedTextbox.Text = "0";
            proccessedErrorTextbox.Text = "0";
            destinationAnalyzedPhotos.Text = "0";
            proccessBox.Visible = true;
            targetDirectoryTextbox.Text = targetDirectoryTextbox.Text.Replace('/', '\\').TrimEnd('\\');

            outputStructureIndex = outputStructureSelect.SelectedIndex;
            fileAlreadyExistsChoice = (FileAlreadyExists)fileExistsSelect.SelectedIndex;
            filesAreSameChoice = (FilesAreSame)filesAreSameSelect.SelectedIndex;

            var t = new Thread(() => processPhotos(action));
            t.Start();
        }

        private void processPhotos(Action action)
        {
            try 
            { 
                if(!Directory.Exists(targetDirectoryTextbox.Text))
                { 
                    Directory.CreateDirectory(targetDirectoryTextbox.Text);
                }

                SetControlPropertyThreadSafe(targetDirectoryTextbox, "Enabled", false);
                SetControlPropertyThreadSafe(destinationFolderButton, "Enabled", false);
                SetControlPropertyThreadSafe(outputStructureSelect, "Enabled", false);
                SetControlPropertyThreadSafe(fileExistsSelect, "Enabled", false);
                SetControlPropertyThreadSafe(filesAreSameSelect, "Enabled", false);
                
                SetControlPropertyThreadSafe(copyPhotosButton, "Enabled", false);
                SetControlPropertyThreadSafe(movePhotosButton, "Enabled", false);
            }
            catch
            {
                MessageBox.Show("Destination directory cannot be created!", "Cannot create destination directory", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetControlPropertyThreadSafe(targetDirectoryTextbox, "Enabled", true);
                SetControlPropertyThreadSafe(destinationFolderButton, "Enabled", true);
                SetControlPropertyThreadSafe(proccessBox, "Visible", false);
                return;
            }

            string destinationPath = targetDirectoryTextbox.Text;
            string ownFormat = ownFormatTextbox.Text;
            foreach (Photo p in photos)
            {
                var path = getNewPath(destinationPath, p, ownFormat);             
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    destinationFolderAnalyse.Add(path, new Dictionary<string, string>());
                }
                else if(filesAreSameChoice == FilesAreSame.ExactFileContentsMatch)
                {
                    analyzeDestinationFolder(path);
                }

                if(prepareCopyOrMove(action, p, path))
                {
                    proccessedPhotos++;
                    SetControlPropertyThreadSafe(proccessedTextbox, "Text", proccessedPhotos.ToString());
                }
                else
                {
                    proccessedError++;
                    SetControlPropertyThreadSafe(proccessedErrorTextbox, "Text", proccessedError.ToString());
                }
            }
        }

        private bool prepareCopyOrMove(Action action, Photo photo, string newPath)
        {
            string duplicate = findDupliate(photo, newPath);
            string destinationPath = Path.Combine(newPath, photo.Name);

            if (!duplicate.Equals(String.Empty))
            {
                switch (fileAlreadyExistsChoice)
                {
                    case FileAlreadyExists.DoNotMoveOrCopy:
                        log.AppendLine("IGNORE FILE - ALREADY EXISTS: " + photo.Path + "> dup(" + duplicate + ")");
                        return true; 
                    case FileAlreadyExists.MoveToSpecifiedDuplicatesFolder:
                        throw new Exception("Not implemented!");
                        break;
                    case FileAlreadyExists.AddTagThenMoveOrCopy:
                        string n = photo.Name;
                        if (FilesAreSame.ExactFileContentsMatch == filesAreSameChoice)
                        {
                            n = Path.GetFileName(duplicate);
                        }
                        destinationPath = GetFreeFileName(newPath, n);
                        break;
                }
            }
            else if(File.Exists(destinationPath))
            {
                string newDestName = GetFreeFileName(newPath, Path.GetFileName(destinationPath), "collision");
                log.AppendLine("Unique content - name colision: " + newDestName + " colisionWith(" + destinationPath + ")");
                destinationPath = newDestName;
            }
            return CopyMove(photo.Path, destinationPath, action);
        }

        private bool CopyMove(string from, string to, Action action)
        {
            try
            {
                switch (action)
                {
                    case Action.Move:
                        if (File.Exists(to))
                        {
                            File.Delete(to);
                        }
                        File.Move(from, to);
                        break;
                    case Action.Copy:
                        File.Copy(from, to, true);
                        break;
                }

                if (filesAreSameChoice == FilesAreSame.ExactFileContentsMatch)
                {
                    AnalyzeFile(to);
                }
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }
        private void AnalyzeFile(string to)
        {
            string directory = Path.GetDirectoryName(to);
            string filename = Path.GetFileName(to);
            var dict = destinationFolderAnalyse[directory];
            int cnt = dict.Count();

            var item = dict.FirstOrDefault(kvp => kvp.Value == filename);
            if (item.Key != null)
            {
                dict.Remove(item.Key);
            }            

            string hash = Crypto.GetHash(to);
            if (dict.ContainsKey(hash))
            {
                dict.Remove(hash);
            }

            dict.Add(hash, filename);

            int diff = dict.Count() - cnt;

            countDestinationAnalyzed += diff;
            SetControlPropertyThreadSafe(destinationAnalyzedPhotos, "Text", countDestinationAnalyzed.ToString());

        }

        private string GetFreeFileName(string path, string name, string param = "copy")
        {
            string free = Path.Combine(path, name);
            if (!File.Exists(Path.Combine(path, name)))
            {
                return free;
            }

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            string newName = "";
            int i = 1;

            string pattern = @"(.*)-(copy|collision)(\d\d\d\d)$";
            MatchCollection mc = Regex.Matches(nameWithoutExtension, pattern);
            if(mc.Count == 1)
            {
                Match m = mc[0];
                string type = m.Groups[2].Value;

                if(type.Equals(param))
                { 
                    nameWithoutExtension = m.Groups[1].Value;
                    i = Convert.ToInt32(m.Groups[3].Value) + 1;
                }
            }
            
            do
            {
                newName = nameWithoutExtension + "-" + param + i.ToString("D4") + extension;
                free = Path.Combine(path, newName);
                i++;
            } while (File.Exists(free));

            return free;
        }

        private string findDupliate(Photo input, string path, string newName = "")
        {
            string output = "";
            switch (filesAreSameChoice)
            {
                case FilesAreSame.FileNameMatch:
                    string f = Path.Combine(path, newName == "" ? Path.GetFileName(input.Path) : newName);
                    if (File.Exists(f))
                    {
                        output = f;
                    }
                    break;
                case FilesAreSame.ExactFileContentsMatch:
                    if (destinationFolderAnalyse.ContainsKey(path) && destinationFolderAnalyse[path].ContainsKey(input.Hash))
                    {
                        output = Path.Combine(path, destinationFolderAnalyse[path][input.Hash]);
                    }
                    break;
            }
            return output;
        }

        private void analyzeDestinationFolder(string targetDirectory)
        {
            if(destinationFolderAnalyse.ContainsKey(targetDirectory))
            {
                return;
            }

            destinationFolderAnalyse.Add(targetDirectory, new Dictionary<string, string>());

            List<string> files = GetAllImagesInDirectory(targetDirectory);
            if (files.Count == 0)
            {
                return;
            }

            foreach (string file in files)
            {
                string c = Path.GetFileName(file);
                string hash = Crypto.GetHash(file);
                if (!destinationFolderAnalyse[targetDirectory].ContainsKey(hash))
                {
                    destinationFolderAnalyse[targetDirectory].Add(hash, c);
                }                
                countDestinationAnalyzed++;
                SetControlPropertyThreadSafe(destinationAnalyzedPhotos, "Text", countDestinationAnalyzed.ToString());
            }

            /*string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
            {
                analyzeDestinationFolder(subdirectory);
            }*/
        }

        private List<string> GetAllImagesInDirectory(string directory)
        {
            List<string> files = new List<string>();
            foreach (var filter in extensions)
            {
                string[] searched = Directory.GetFiles(directory, String.Format("*.{0}", filter));
                files.AddRange(searched);
            }
            return files;
        }

        private bool arePhotosSameContent(Photo p1, Photo p2)
        {
            return CompareHashes(p1.Hash, p2.Hash);
        }

        private string getNewPath(string target, Photo photo, string ownformat = "")
        {
            string pattern = "";
            switch (outputStructureIndex)
            {
                case 0:
                    pattern = "%y\\%m\\%d";
                    break;
                case 1:
                    pattern = "%y\\%m\\%d\\%M";
                    break;
                case 2:
                    pattern = "%y\\%m";
                    break;
                case 3:
                    pattern = "%y\\%m\\%M";
                    break;
                case 4:
                    pattern = "%y";
                    break;
                case 5:
                    pattern = "%y\\%M";
                    break;
                case 6:
                    pattern = "%m";
                    break;
                case 7:
                    pattern = "%m\\%M";
                    break;
                case 8:
                    pattern = "%d";
                    break;
                case 9:
                    pattern = "%d\\%M";
                    break;
                case 10:
                    pattern = "%M";
                    break;
                case 11:
                    pattern = "%M\\%y\\%m\\%d";
                    break;
                case 12:
                    pattern = "%M\\%y\\%m";
                    break;
                case 13:
                    pattern = "%M\\%y";
                    break;
                case 14:
                    pattern = "%M\\%m";
                    break;
                case 15:
                    pattern = "%M\\%d";
                    break;
                case 16:
                    pattern = ownformat;
                    break;
                default:
                    return String.Empty;
            }

            string path = pattern.Replace("%y", photo.Taken.Year.ToString("D4"))
                                 .Replace("%m", photo.Taken.Month.ToString("D2"))
                                 .Replace("%d", photo.Taken.Day.ToString("D2"))
                                 .Replace("%h", photo.Taken.Hour.ToString("D2"))
                                 .Replace("%i", photo.Taken.Minute.ToString("D2"))
                                 .Replace("%s", photo.Taken.Second.ToString("D2"))
                                 .Replace("%f", photo.Manufacturer.Trim())
                                 .Replace("%M", photo.Model.Trim());

            MatchCollection mc = Regex.Matches(path, @"[\<\>\:\""\/\|\?\*\%]");
            if (mc.Count > 0)
            {
                throw new Exception("Invalid path format: '"+ path + "'.");
            }
            return Path.Combine(target, path);
        }

        private void outputStructureSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
                ownFormatTextbox.Visible = ownFormatLabel.Visible = (outputStructureSelect.SelectedIndex == 16);
        }

        private void fileExistsSelect_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
