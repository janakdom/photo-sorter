using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoSorting
{
    [Serializable]
    public class Configuration
    {
        public string sourceDirectory { get; set; }
        public string targetDirectory { get; set; }
        public bool subFolders { get; set; }
        public int outputStructure { get; set; }
        public string ownFormat { get; set; }
        public FileAlreadyExists fileAlreadyExists { get; set; }
        public FilesAreSame filesAreSame { get; set; }
        public string fileTypes { get; set; }
    }
}
