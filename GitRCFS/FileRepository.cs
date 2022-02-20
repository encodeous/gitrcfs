using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace GitRCFS
{
    public class FileRepository : RcfsNode, IDisposable
    {
        private readonly Repository _repo;
        private readonly Remote _remote;
        private readonly CredentialsHandler _cred;
        private readonly FetchOptions _fetchOptions;
        private bool _isDisposed = false;
        
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
        /// <param name="accessToken">Git authentication token, (if nessecary)</param>
        /// <param name="updateFrequencyMs">How frequently to update the repository, set to -1 to disable</param>
        public FileRepository(string repoUrl, string branch = "main", string accessToken = null, int updateFrequencyMs = 30000) : base(true, "rcfs-root-node")
        {
            _cred = (url, fromUrl, types) => 
                new UsernamePasswordCredentials()
                {
                    Username = accessToken,
                    Password = ""
                };
            Branch = branch;
            var repoHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(repoUrl)));
            rootPath = ".gitrcfs-" + repoHash.Substring(0, 10) + "-" + branch;
            _fetchOptions = new FetchOptions();
            _fetchOptions.CredentialsProvider = _cred;
            _fetchOptions.Prune = true;
            if (!Directory.Exists(rootPath))
            {
                var co = new CloneOptions();
                co.IsBare = false;
                co.RecurseSubmodules = true;
                co.FetchOptions = _fetchOptions;
                co.BranchName = branch;
                if (accessToken is not null)
                {
                    co.CredentialsProvider = _cred;
                }

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
            var refSpecs = _remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(_repo, _remote.Name, refSpecs, _fetchOptions, "");
            var br = _repo.Branches[_remote.Name + "/" + Branch];
            _repo.Reset(ResetMode.Hard, br.Tip);
            _repo.RemoveUntrackedFiles();
            if (Commit != _repo.Head.Tip.Sha)
            {
                Commit = _repo.Head.Tip.Sha;
                ApplyChanges();
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