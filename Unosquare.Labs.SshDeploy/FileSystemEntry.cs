namespace Unosquare.Labs.SshDeploy
{
    using System;
    using System.IO;

    /// <summary>
    /// Represents a trackable file system entry
    /// </summary>
    public class FileSystemEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemEntry"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        public FileSystemEntry(string path)
        {
            var info = new FileInfo(path);
            Path = info.DirectoryName;
            Filename = info.Name;
            Size = info.Length;
            DateCreatedUtc = info.CreationTimeUtc;
            DateModifiedUtc = info.LastWriteTimeUtc;
        }

        public string Filename { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime DateCreatedUtc { get; set; }
        public DateTime DateModifiedUtc { get; set; }
    }
}