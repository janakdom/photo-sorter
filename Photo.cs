using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PhotoSorting
{
    class Photo
    {
        private string _hash = "";

        public string Path { get; set; }
        public string Extension {
            get => System.IO.Path.GetExtension(Path);
        }
        public string Name {
            get => System.IO.Path.GetFileName(Path);
        }
        public DateTime Taken { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string Hash
        {
            get => _hash.Length > 0 ? _hash : CalculateHash();
        }

        private string CalculateHash()
        {
            _hash = Crypto.GetHash(Path);
            return _hash;
        }
    }
}
