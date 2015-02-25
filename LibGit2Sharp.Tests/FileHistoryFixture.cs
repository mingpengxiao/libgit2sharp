using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp.Tests.TestHelpers;
using Xunit;
using Xunit.Extensions;

namespace LibGit2Sharp.Tests
{
    public class FileHistoryFixture : BaseFixture
    {
        [Fact]
        public void EmptyRepositoryHasNoHistory()
        {
            string repoPath = CreateEmptyRepository();

            using (Repository repo = new Repository(repoPath))
            {
                IEnumerable<FileHistoryEntry> history = repo.Follow("Test.txt");
                Assert.Equal(0, history.Count());
                Assert.Equal(0, history.ChangedBlobs().Count());
            }
        }

        [Fact]
        public void CanTellSingleCommitHistory()
        {
            string repoPath = CreateEmptyRepository();

            using (Repository repo = new Repository(repoPath))
            {
                // Set up repository.
                string path = "Test.txt";
                Commit commit = MakeAndCommitChange(repo, repoPath, path, "Hello World");

                // Perform tests.
                IEnumerable<FileHistoryEntry> history = repo.Follow(path);
                IEnumerable<Blob> changedBlobs = history.ChangedBlobs();

                Assert.Equal(1, history.Count());
                Assert.Equal(1, changedBlobs.Count());

                Assert.Equal(path, history.First().Path);
                Assert.Equal(commit, history.First().Commit);
            }
        }

        [Fact]
        public void CanTellSimpleCommitHistory()
        {
            string repoPath = CreateEmptyRepository();
            string path1 = "Test1.txt";
            string path2 = "Test2.txt";

            using (Repository repo = new Repository(repoPath))
            {
                // Set up repository.
                Commit commit1 = MakeAndCommitChange(repo, repoPath, path1, "Hello World");
                Commit commit2 = MakeAndCommitChange(repo, repoPath, path2, "Second file's contents");
                Commit commit3 = MakeAndCommitChange(repo, repoPath, path1, "Hello World again");

                // Perform tests.
                IEnumerable<FileHistoryEntry> history = repo.Follow(path1);
                IEnumerable<Blob> changedBlobs = history.ChangedBlobs();

                Assert.Equal(2, history.Count());
                Assert.Equal(2, changedBlobs.Count());

                Assert.Equal(commit3, history.ElementAt(0).Commit);
                Assert.Equal(commit1, history.ElementAt(1).Commit);
            }
        }

        [Fact]
        public void CanTellComplexCommitHistory()
        {
            string repoPath = CreateEmptyRepository();
            string path1 = "Test1.txt";
            string path2 = "Test2.txt";

            using (Repository repo = new Repository(repoPath))
            {
                // Make initial changes.
                Commit commit1 = MakeAndCommitChange(repo, repoPath, path1, "Hello World");
                MakeAndCommitChange(repo, repoPath, path2, "Second file's contents");
                Commit commit2 = MakeAndCommitChange(repo, repoPath, path1, "Hello World again");

                // Move the first file to a new directory.
                string newPath1 = Path.Combine(subFolderPath1, path1);
                repo.Move(path1, newPath1);
                Commit commit3 = repo.Commit("Moved " + path1 + " to " + newPath1,
                    Constants.Signature, Constants.Signature);

                // Make further changes.
                MakeAndCommitChange(repo, repoPath, path2, "Changed second file's contents");
                Commit commit4 = MakeAndCommitChange(repo, repoPath, newPath1, "I have done it again!");

                // Perform tests.
                List<FileHistoryEntry> fileHistoryEntries = repo.Follow(newPath1).ToList();
                List<Blob> changedBlobs = fileHistoryEntries.ChangedBlobs().ToList();

                Assert.Equal(4, fileHistoryEntries.Count());
                Assert.Equal(3, changedBlobs.Count());

                Assert.Equal(2, fileHistoryEntries.Where(e => e.Path == newPath1).Count());
                Assert.Equal(2, fileHistoryEntries.Where(e => e.Path == path1).Count());

                Assert.Equal(commit4, fileHistoryEntries[0].Commit);
                Assert.Equal(commit3, fileHistoryEntries[1].Commit);
                Assert.Equal(commit2, fileHistoryEntries[2].Commit);
                Assert.Equal(commit1, fileHistoryEntries[3].Commit);

                Assert.Equal(commit4.Tree[newPath1].Target, changedBlobs[0]);
                Assert.Equal(commit2.Tree[path1].Target, changedBlobs[1]);
                Assert.Equal(commit1.Tree[path1].Target, changedBlobs[2]);
            }
        }

        [Theory]
        [InlineData("https://github.com/nulltoken/follow-test.git")]
        public void CanDealWithFollowTest(string url)
        {
            var scd = BuildSelfCleaningDirectory();
            string clonedRepoPath = Repository.Clone(url, scd.DirectoryPath);

            using (Repository repo = new Repository(clonedRepoPath))
            {
                // $ git log --follow --format=oneline so-renamed.txt
                // 88f91835062161febb46fb270ef4188f54c09767 Update not-yet-renamed.txt AND rename into so-renamed.txt
                // ef7cb6a63e32595fffb092cb1ae9a32310e58850 Add not-yet-renamed.txt
                List<FileHistoryEntry> fileHistoryEntries = repo.Follow("so-renamed.txt").ToList();
                Assert.Equal(2, fileHistoryEntries.Count());
                Assert.Equal(1, fileHistoryEntries.ExcludeRenames().Count());
                Assert.Equal("88f91835062161febb46fb270ef4188f54c09767", fileHistoryEntries[0].Commit.Sha);
                Assert.Equal("ef7cb6a63e32595fffb092cb1ae9a32310e58850", fileHistoryEntries[1].Commit.Sha);

                // $ git log --follow --format=oneline untouched.txt
                // c10c1d5f74b76f20386d18674bf63fbee6995061 Initial commit
                fileHistoryEntries = repo.Follow("untouched.txt").ToList();
                Assert.Equal(1, fileHistoryEntries.Count());
                Assert.Equal(1, fileHistoryEntries.ExcludeRenames().Count());
                Assert.Equal("c10c1d5f74b76f20386d18674bf63fbee6995061", fileHistoryEntries[0].Commit.Sha);

                // $ git log --follow --format=oneline under-test.txt
                // 0b5b18f2feb917dee98df1210315b2b2b23c5bec Rename file renamed.txt into under-test.txt
                // 49921d463420a892c9547a326632ef6a9ba3b225 Update file renamed.txt
                // 70f636e8c64bbc2dfef3735a562bb7e195d8019f Rename file under-test.txt into renamed.txt
                // d3868d57a6aaf2ae6ed4887d805ae4bc91d8ce4d Updated file under test
                // 9da10ef7e139c49604a12caa866aae141f38b861 Updated file under test
                // 599a5d821fb2c0a25855b4233e26d475c2fbeb34 Updated file under test
                // 678b086b44753000567aa64344aa0d8034fa0083 Updated file under test
                // 8f7d9520f306771340a7c79faea019ad18e4fa1f Updated file under test
                // bd5f8ee279924d33be8ccbde82e7f10b9d9ff237 Updated file under test
                // c10c1d5f74b76f20386d18674bf63fbee6995061 Initial commit
                fileHistoryEntries = repo.Follow("under-test.txt").ToList();
                Assert.Equal(10, fileHistoryEntries.Count());
                Assert.Equal(1, fileHistoryEntries.ExcludeRenames().Count());
                Assert.Equal("0b5b18f2feb917dee98df1210315b2b2b23c5bec", fileHistoryEntries[0].Commit.Sha);
                Assert.Equal("49921d463420a892c9547a326632ef6a9ba3b225", fileHistoryEntries[1].Commit.Sha);
                Assert.Equal("70f636e8c64bbc2dfef3735a562bb7e195d8019f", fileHistoryEntries[2].Commit.Sha);
                Assert.Equal("d3868d57a6aaf2ae6ed4887d805ae4bc91d8ce4d", fileHistoryEntries[3].Commit.Sha);
                Assert.Equal("9da10ef7e139c49604a12caa866aae141f38b861", fileHistoryEntries[4].Commit.Sha);
                Assert.Equal("599a5d821fb2c0a25855b4233e26d475c2fbeb34", fileHistoryEntries[5].Commit.Sha);
                Assert.Equal("678b086b44753000567aa64344aa0d8034fa0083", fileHistoryEntries[6].Commit.Sha);
                Assert.Equal("8f7d9520f306771340a7c79faea019ad18e4fa1f", fileHistoryEntries[7].Commit.Sha);
                Assert.Equal("bd5f8ee279924d33be8ccbde82e7f10b9d9ff237", fileHistoryEntries[8].Commit.Sha);
                Assert.Equal("c10c1d5f74b76f20386d18674bf63fbee6995061", fileHistoryEntries[9].Commit.Sha);
            }
        }

        [Theory]
        [InlineData(null)]
        public void CanFollowBranches(string specificRepoPath)
        {
            string repoPath = specificRepoPath ?? CreateEmptyRepository();
            string path = "Test.txt";

            using (Repository repo = new Repository(repoPath))
            {
                List<Commit> commits = new List<Commit>();
                var dummy = "\n" + new string('a', 1024) +"\n";
                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Before merge" + dummy, "0. Initial commit for this test"));

                Branch fixBranch = repo.CreateBranch("fix", GetNextSignature());

                repo.Checkout("fix");
                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Change on fix branch" + dummy, "1. Changed on fix"));

                repo.Checkout("master");
                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Independent change on master branch" + dummy, "2. Changed on master"));

                repo.Checkout("fix");
                var oldpath = path;
                path += ".new";
                repo.Move(oldpath, path);
                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Another change on fix branch" + dummy, "3. Changed on fix"));

                repo.Checkout("master");
                repo.Move(oldpath, path);
                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Another independent change on master branch" + dummy, "4. Changed on master"));

                MergeResult mergeResult = repo.Merge("fix", GetNextSignature());
                if (mergeResult.Status == MergeStatus.Conflicts)
                {
                    repo.Index.Remove(oldpath);
                    commits.Add(MakeAndCommitChange(repo, repoPath, path, "Manual resolution of merge conflict", "5. Merged fix into master"));
                }

                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Change after merge" + dummy, "6. Changed on master"));

                repo.CreateBranch("next-fix", GetNextSignature());

                repo.Checkout("next-fix");
                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Change on next-fix branch" + dummy, "7. Changed on next-fix"));

                repo.Checkout("master");
                commits.Add(MakeAndCommitChange(repo, repoPath, path, "Some arbitrary change on master branch" + dummy, "8. Changed on master"));

                mergeResult = repo.Merge("next-fix", GetNextSignature());
                if (mergeResult.Status == MergeStatus.Conflicts)
                {
                    commits.Add(MakeAndCommitChange(repo, repoPath, path, "Another manual resolution of merge conflict" + dummy, "9. Merged next-fix into master"));
                }

                commits.Add(MakeAndCommitChange(repo, repoPath, path, "A change on master after merging" + dummy, "10. Changed on master"));

                // Test --date-order.
                IEnumerable<FileHistoryEntry> timeHistory = repo.Follow(path, new CommitFilter { SortBy = CommitSortStrategies.Time });
                List<Commit> timeCommits = new List<Commit>
                {
                    commits[10],    // master

                    commits[8],     // master
                        commits[7],     // next-fix
                    commits[6],     // master

                    commits[4],     // master
                        commits[3],     // fix
                    commits[2],     // master
                        commits[1],     // fix
                    commits[0]      // master (initial commit)
                };
                Assert.Equal<Commit>(timeCommits, timeHistory.Select(e => e.Commit));
                Assert.Equal(timeHistory.Count(), timeHistory.ExcludeRenames().Count());

                // Test --topo-order.
                IEnumerable<FileHistoryEntry> topoHistory = repo.Follow(path, new CommitFilter { SortBy = CommitSortStrategies.Topological });
                List<Commit> topoCommits = new List<Commit>
                {
                    commits[10],    // master

                        commits[7],     // next-fix
                    commits[8],     // master
                    commits[6],     // master

                        commits[3],     // fix
                        commits[1],     // fix
                    commits[4],     // master
                    commits[2],     // master
                    commits[0]      // master (initial commit)
                };
                Assert.Equal<Commit>(topoCommits, topoHistory.Select(e => e.Commit));
                Assert.Equal(topoHistory.Count(), topoHistory.ExcludeRenames().Count());
            }
        }

        #region Helpers

        protected Signature signature = Constants.Signature;
        protected string subFolderPath1 = "SubFolder1";

        protected Signature GetNextSignature()
        {
            signature = signature.TimeShift(TimeSpan.FromMinutes(1));
            return signature;
        }

        protected string CreateEmptyRepository()
        {
            // Create a new empty directory with subfolders.
            SelfCleaningDirectory scd = BuildSelfCleaningDirectory();
            Directory.CreateDirectory(Path.Combine(scd.DirectoryPath, subFolderPath1));

            // Initialize a GIT repository in that directory.
            Repository.Init(scd.DirectoryPath, false);
            using (Repository repo = new Repository(scd.DirectoryPath))
            {
                repo.Config.Set("user.name", signature.Name);
                repo.Config.Set("user.email", signature.Email);
            }

            // Done.
            return scd.DirectoryPath;
        }

        protected Commit MakeAndCommitChange(Repository repo, string repoPath, string path, string text, string message = null)
        {
            Touch(repoPath, path, text);
            repo.Stage(path);

            Signature commitSignature = GetNextSignature();
            return repo.Commit(message ?? "Changed " + path, commitSignature, commitSignature);
        }

        #endregion
    }
}
