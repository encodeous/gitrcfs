using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitRCFS
{
    public class FileRepository : RcfsNode, IDisposable
    {
        private readonly Repository _repo;
        private readonly Remote _remote;
        private readonly CredentialsHandler _cred;
        private readonly FetchOptions _fetchOptions;
        private bool _isDisposed = false;
        private ILogger<FileRepository> _logger;
        
        /// <summary>
        /// The current commit
        /// </summary>
        public string Commit { get; private set; }
        /// <summary>
        /// The current branch
        /// </summary>
        public readonly string Branch;

        /// <summary>
        /// Creates a git repository that reflects the changes made to remote.
        /// </summary>
        /// <param name="repoUrl">Remote repository url</param>
        /// <param name="branch">Branch Name</param>
        /// <param name="username">Git authentication username, (if necessary)</param>
        /// <param name="password">Git authentication password, (if necessary)</param>
        /// <param name="updateFrequencyMs">How frequently to update the repository, set to -1 to disable</param>
        /// <param name="logger">configure a logger, as opposed to the default null logger</param>
        public FileRepository(string repoUrl, string branch = "main", string username = null, string password = null, int updateFrequencyMs = 30000, ILogger<FileRepository> logger = null) : base(true, "rcfs-root-node", logger)
        {
            if (logger is null)
                logger = new NullLogger<FileRepository>();

            _logger = logger;
            
            _cred = (_, _, _) => 
                new UsernamePasswordCredentials()
                {
                    Username = username,
                    Password = password
                };
            Branch = branch;
            var repoHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(repoUrl)));
            rootPath = ".gitrcfs-" + repoHash.Substring(0, 10) + "-" + branch;
            _fetchOptions = new FetchOptions();
            if(username != null && password != null)
                _fetchOptions.CredentialsProvider = _cred;
            _fetchOptions.Prune = true;
            if (!Directory.Exists(rootPath))
            {
                _logger.LogDebug("Creating RCFS folder from \"{Path}\"", repoUrl);
                var co = new CloneOptions();
                co.IsBare = false;
                co.RecurseSubmodules = true;
                if(username != null && password != null)
                    co.FetchOptions.CredentialsProvider = _cred;
                co.FetchOptions.Prune = true;
                co.BranchName = branch;

                _logger.LogDebug("Cloning contents");
                Repository.Clone(repoUrl, rootPath, co);
            }

            _repo = new Repository(rootPath);
            _remote = _repo.Network.Remotes["origin"];
            Update();
            
            if (updateFrequencyMs != -1)
            {
                Task.Run(async () =>
                {
                    while (!_isDisposed)
                    {
                        await Task.Delay(updateFrequencyMs);
                        _logger.LogDebug("Updating RCFS folder contents");
                        Update();
                    }
                });
            }
        }

        /// <summary>
        /// Updates the local repository to reflect the changes to the remote one
        /// </summary>
        public void Update()
        {
            try
            {
                var refSpecs = _remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(_repo, _remote.Name, refSpecs, _fetchOptions, "");
                var br = _repo.Branches[_remote.Name + "/" + Branch];
                _repo.Reset(ResetMode.Hard, br.Tip);
                _repo.RemoveUntrackedFiles();
                if (Commit != _repo.Head.Tip.Sha)
                {
                    _logger.LogDebug("RCFS Head SHA changed from {CurCommit} to {NewCommit}", Commit, _repo.Head.Tip.Sha);
                    Commit = _repo.Head.Tip.Sha;
                    ApplyChanges();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed updating RCFS repository {Path}", _remote.Url);
            }
        }

        /// <summary>
        /// Dispose the class and stop checking for updates
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _repo?.Dispose();
                _remote?.Dispose();
            }
        }
    }
}