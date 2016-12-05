namespace Unosquare.Labs.SshDeploy
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    /// <summary>
    /// Represents the different change types that the
    /// FileSystemMonitor class can detect
    /// </summary>
    public enum FileSystemEntryChangeType
    {
        FileAdded,
        FileRemoved,
        FileModified,
    }

    /// <summary>
    /// Respresnets change data for a file system entry
    /// </summary>
    public class FileSystemEntryChangedEventArgs : EventArgs
    {
        public FileSystemEntryChangedEventArgs(FileSystemEntryChangeType changeType, string path)
            : base()
        {
            this.ChangeType = changeType;
            this.Path = path;
        }

        public FileSystemEntryChangeType ChangeType { get; private set; }
        public string Path { get; private set; }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0}: {1}", ChangeType, Path);
        }
    }

    /// <summary>
    /// Represents a trackable file system entry
    /// </summary>
    public class FileSystemEntry
    {
        /// <summary>
        /// Initializes a new instance of this class.
        /// The path needs to point to a file.
        /// </summary>
        /// <param name="path"></param>
        public FileSystemEntry(string path)
        {
            var info = new System.IO.FileInfo(path);
            this.Path = info.DirectoryName;
            this.Filename = info.Name;
            this.Size = info.Length;
            this.DateCreatedUtc = info.CreationTimeUtc;
            this.DateModifiedUtc = info.LastWriteTimeUtc;
        }

        public string Filename { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime DateCreatedUtc { get; set; }
        public DateTime DateModifiedUtc { get; set; }
    }

    /// <summary>
    /// A dictionary of file system entries. Keys are string paths, values are file system entries
    /// </summary>
    public class FileSystemEntryDictionary : Dictionary<string, FileSystemEntry>
    {
        /// <summary>
        /// Initializes a new instance of this class
        /// </summary>
        public FileSystemEntryDictionary()
            : base(1024, StringComparer.InvariantCultureIgnoreCase)
        {
            // placeholder
        }
    }

    /// <summary>
    /// Represents a long-running file system monitor based on polling
    /// FileSystemWatcher does not handle some scenarios well enough and this is why
    /// a custom monitor was implemented. This class is not meant for monitoring a large
    /// amount of file or directories. In other words, do not monitor the root of a drive,
    /// or a folder with thousands of files.
    /// </summary>
    public class FileSystemMonitor
    {
        public const string AllFilesPattern = "*.*";
        private readonly FileSystemEntryDictionary Entries = new FileSystemEntryDictionary();
        private BackgroundWorker Worker = null;
        public delegate void FileSystemEntryChangedHandler(object sender, FileSystemEntryChangedEventArgs e);
        public event FileSystemEntryChangedHandler FileSystemEntryChanged;

        /// <summary>
        /// Creates a new instanceof the FileSystemMonitor class
        /// </summary>
        /// <param name="pollIntervalSeconds"></param>
        /// <param name="fileSystemPath"></param>
        public FileSystemMonitor(int pollIntervalSeconds, string fileSystemPath)
            : base()
        {
            this.PollIntervalSeconds = pollIntervalSeconds;
            this.FileSystemPath = fileSystemPath;
            this.Worker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            this.Worker.DoWork += DoWork;
        }

        /// <summary>
        /// The polling interval in seconds at which the file system is monitored for changes
        /// </summary>
        public int PollIntervalSeconds { get; private set; }

        /// <summary>
        /// The root path that is monitored for changes
        /// </summary>
        public string FileSystemPath { get; private set; }

        /// <summary>
        /// Raises the FileSystemEntryChanged Event
        /// </summary>
        /// <param name="changeType"></param>
        /// <param name="path"></param>
        private void RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType changeType, string path)
        {
            if (this.FileSystemEntryChanged != null)
                this.FileSystemEntryChanged(this, new FileSystemEntryChangedEventArgs(changeType, path));
        }


        /// <summary>
        /// Stops the File System Monitor
        /// This is a blocking call
        /// </summary>
        public void Stop()
        {
            if (Worker.CancellationPending)
                return;

            Worker.CancelAsync();
            this.ClearMonitorEntries();
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Service is already running.</exception>
        public virtual void Start()
        {
            if (Worker.IsBusy)
                throw new InvalidOperationException("Service is already running.");

            Worker.RunWorkerAsync();
        }

        /// <summary>
        /// Does the work when the Start method is called.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        protected void DoWork(object sender, DoWorkEventArgs e)
        {
            const int minimumInterval = 1;
            const int maximumInterval = 60;

            // validate argumets
            if (this.PollIntervalSeconds < minimumInterval || this.PollIntervalSeconds > maximumInterval)
                throw new ArgumentException("PollIntervalSeconds must be between 2 and 60");

            if (System.IO.Directory.Exists(this.FileSystemPath) == false)
                throw new ArgumentException("Configuration item InputFolderPath does not point to a valid folder");

            // normalize file system path parameter
            this.FileSystemPath = System.IO.Path.GetFullPath(this.FileSystemPath);

            // Only new files shall be taken into account.
            this.InitializeMonitorEntries();

            // keep track of a timout interval
            var lastPollTime = DateTime.Now;
            while (!Worker.CancellationPending)
            {
                try
                {
                    // check for polling interval before processing changes
                    if (DateTime.Now.Subtract(lastPollTime).TotalSeconds > this.PollIntervalSeconds)
                    {
                        lastPollTime = DateTime.Now;
                        this.ProcessMonitorEntryChanges();
                        Worker.ReportProgress(1, new DateTime?(DateTime.Now));
                    }
                }
                catch (Exception ex)
                {
                    // Report the exception
                    Worker.ReportProgress(0, ex);
                }
                finally
                {
                    // sleep some so we don't overload the CPU
                    System.Threading.Thread.Sleep(10);
                }
            }


        }

        /// <summary>
        /// We don't want to fire events for files that were there before we started the worker
        /// </summary>
        private void InitializeMonitorEntries()
        {
            this.ClearMonitorEntries();
            var files = System.IO.Directory.GetFiles(this.FileSystemPath, AllFilesPattern, System.IO.SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var entry = new FileSystemEntry(file);
                    this.Entries[file] = entry;
                }
                catch
                {
                    // swallow; no access
                }
            }
        }

        /// <summary>
        /// Compares what we are tracking under Entries and what the file system reports
        /// Based on such comparisons it raises the necessary events
        /// </summary>
        private void ProcessMonitorEntryChanges()
        {
            var files = System.IO.Directory.GetFiles(this.FileSystemPath, AllFilesPattern, System.IO.SearchOption.AllDirectories);

            // check for any missing files
            var existingKeys = this.Entries.Keys.ToArray();
            foreach (var existingKey in existingKeys)
            {
                if (files.Any(f => f.Equals(existingKey, StringComparison.InvariantCultureIgnoreCase)) == false)
                {
                    this.Entries.Remove(existingKey);
                    this.RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType.FileRemoved, existingKey);
                }
            }

            // now, compare each entry and add the new ones (if any)
            foreach (var file in files)
            {
                try
                {
                    var entry = new FileSystemEntry(file);
                    // in the case we already have it in the tracking collection
                    if (this.Entries.ContainsKey(file))
                    {
                        var existingEntry = this.Entries[file];
                        if (existingEntry.DateCreatedUtc != entry.DateCreatedUtc ||
                            existingEntry.DateModifiedUtc != entry.DateModifiedUtc ||
                            existingEntry.Size != entry.Size)
                        {
                            // update the entry and raise the change event
                            this.Entries[file] = entry;
                            this.RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType.FileModified, file);
                        }

                    }
                    // in the case we do not have it in the tracking collection
                    else
                    {
                        // add the entry and raise the added event
                        this.Entries[file] = entry;
                        this.RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType.FileAdded, file);
                    }

                }
                catch
                {
                    // swallow
                }
            }
        }

        /// <summary>
        /// Clears all the dictionary entries.
        /// This method is used when we startup or reset the file system monitor
        /// </summary>
        private void ClearMonitorEntries()
        {
            this.Entries.Clear();
        }
    }
}