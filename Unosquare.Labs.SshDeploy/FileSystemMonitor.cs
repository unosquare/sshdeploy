namespace Unosquare.Labs.SshDeploy
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Represents a long-running file system monitor based on polling
    /// FileSystemWatcher does not handle some scenarios well enough and this is why
    /// a custom monitor was implemented. This class is not meant for monitoring a large
    /// amount of file or directories. In other words, do not monitor the root of a drive,
    /// or a folder with thousands of files.
    /// </summary>
    internal class FileSystemMonitor
    {
        public const string AllFilesPattern = "*.*";
        private readonly FileSystemEntryDictionary _entries = new FileSystemEntryDictionary();
        private readonly BackgroundWorker _worker;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemMonitor"/> class.
        /// </summary>
        /// <param name="pollIntervalSeconds">The poll interval seconds.</param>
        /// <param name="fileSystemPath">The file system path.</param>
        public FileSystemMonitor(int pollIntervalSeconds, string fileSystemPath)
        {
            PollIntervalSeconds = pollIntervalSeconds;
            FileSystemPath = fileSystemPath;
            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true,
            };

            _worker.DoWork += DoWork;
        }

        public delegate void FileSystemEntryChangedHandler(object sender, FileSystemEntryChangedEventArgs e);

        public event FileSystemEntryChangedHandler FileSystemEntryChanged;

        /// <summary>
        /// The polling interval in seconds at which the file system is monitored for changes.
        /// </summary>
        public int PollIntervalSeconds { get; }

        /// <summary>
        /// The root path that is monitored for changes.
        /// </summary>
        public string FileSystemPath { get; private set; }
        
        /// <summary>
        /// Stops the File System Monitor
        /// This is a blocking call.
        /// </summary>
        public void Stop()
        {
            if (_worker.CancellationPending)
                return;

            _worker.CancelAsync();
            ClearMonitorEntries();
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Service is already running.</exception>
        public virtual void Start()
        {
            if (_worker.IsBusy)
                throw new InvalidOperationException("Service is already running.");

            _worker.RunWorkerAsync();
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

            // validate arguments
            if (PollIntervalSeconds < minimumInterval || PollIntervalSeconds > maximumInterval)
                throw new ArgumentException("PollIntervalSeconds must be between 2 and 60");

            if (Directory.Exists(FileSystemPath) == false)
                throw new ArgumentException("Configuration item InputFolderPath does not point to a valid folder");

            // normalize file system path parameter
            FileSystemPath = Path.GetFullPath(FileSystemPath);

            // Only new files shall be taken into account.
            InitializeMonitorEntries();

            // keep track of a timeout interval
            var lastPollTime = DateTime.Now;
            while (!_worker.CancellationPending)
            {
                try
                {
                    // check for polling interval before processing changes
                    if (DateTime.Now.Subtract(lastPollTime).TotalSeconds > PollIntervalSeconds)
                    {
                        lastPollTime = DateTime.Now;
                        ProcessMonitorEntryChanges();
                        _worker.ReportProgress(1, DateTime.Now);
                    }
                }
                catch (Exception ex)
                {
                    // Report the exception
                    _worker.ReportProgress(0, ex);
                }
                finally
                {
                    // sleep some so we don't overload the CPU
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// Raises the file system entry changed event.
        /// </summary>
        /// <param name="changeType">Type of the change.</param>
        /// <param name="path">The path.</param>
        private void RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType changeType, string path)
        {
            FileSystemEntryChanged?.Invoke(this, new FileSystemEntryChangedEventArgs(changeType, path));
        }

        /// <summary>
        /// We don't want to fire events for files that were there before we started the worker.
        /// </summary>
        private void InitializeMonitorEntries()
        {
            ClearMonitorEntries();
            var files = Directory.GetFiles(FileSystemPath, AllFilesPattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    var entry = new FileSystemEntry(file);
                    _entries[file] = entry;
                }
                catch
                {
                    // swallow; no access
                }
            }
        }

        /// <summary>
        /// Compares what we are tracking under Entries and what the file system reports
        /// Based on such comparisons it raises the necessary events.
        /// </summary>
        private void ProcessMonitorEntryChanges()
        {
            var files = Directory.GetFiles(FileSystemPath, AllFilesPattern, SearchOption.AllDirectories);

            // check for any missing files
            var existingKeys = _entries.Keys.ToArray();
            foreach (var existingKey in existingKeys)
            {
                if (files.Any(f => f.Equals(existingKey, StringComparison.InvariantCultureIgnoreCase)) == false)
                {
                    _entries.Remove(existingKey);
                    RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType.FileRemoved, existingKey);
                }
            }

            // now, compare each entry and add the new ones (if any)
            foreach (var file in files)
            {
                try
                {
                    var entry = new FileSystemEntry(file);

                    if (_entries.ContainsKey(file))
                    {
                        // in the case we already have it in the tracking collection
                        var existingEntry = _entries[file];

                        if (existingEntry.DateCreatedUtc != entry.DateCreatedUtc ||
                            existingEntry.DateModifiedUtc != entry.DateModifiedUtc ||
                            existingEntry.Size != entry.Size)
                        {
                            // update the entry and raise the change event
                            _entries[file] = entry;
                            RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType.FileModified, file);
                        }
                    }
                    else
                    {
                        // add the entry and raise the added event
                        _entries[file] = entry;
                        RaiseFileSystemEntryChangedEvent(FileSystemEntryChangeType.FileAdded, file);
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
        /// This method is used when we startup or reset the file system monitor.
        /// </summary>
        private void ClearMonitorEntries() => _entries.Clear();
    }
}