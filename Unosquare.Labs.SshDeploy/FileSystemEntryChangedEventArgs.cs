namespace Unosquare.Labs.SshDeploy
{
    using System;

    internal class FileSystemEntryChangedEventArgs : EventArgs
    {
        public FileSystemEntryChangedEventArgs(FileSystemEntryChangeType changeType, string path)
        {
            ChangeType = changeType;
            Path = path;
        }

        public FileSystemEntryChangeType ChangeType { get; }
        public string Path { get; }

        public override string ToString() => $"{ChangeType}: {Path}";
    }
}