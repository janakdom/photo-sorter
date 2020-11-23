using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoSorting
{
    public enum FileAlreadyExists
    {
        DoNotMoveOrCopy,
        AddTagThenMoveOrCopy,
        OverwireExistingFile,
        MoveToSpecifiedDuplicatesFolder
    }
}
