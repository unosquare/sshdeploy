namespace Unosquare.Labs.SshDeploy
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A dictionary of file system entries. Keys are string paths, values are file system entries.
    /// </summary>
    public class FileSystemEntryDictionary : Dictionary<string, FileSystemEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemEntryDictionary"/> class.
        /// </summary>
        public FileSystemEntryDictionary()
            : base(1024, StringComparer.InvariantCultureIgnoreCase)
        {
            // placeholder
        }
    }
}