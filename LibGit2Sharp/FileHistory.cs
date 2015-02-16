using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp.Core;

namespace LibGit2Sharp
{
    /// <summary>
    /// Defines extensions related to file history.
    /// Note: Some of these could potentially be moved to the RepositoryExtensions class.
    /// </summary>
    public static class FileHistoryExtensions
    {
        /// <summary>
        /// Gets the history of the file having the given <code>path</code> in the
        /// latest commits.
        /// </summary>
        /// <param name="repo">The repository.</param>
        /// <param name="path">The file's path relative to the repository's root.</param>
        /// <returns>The file's full history, including renames.</returns>
        /// <exception cref="ArgumentNullException">When any of the parameters is null.</exception>
        public static IEnumerable<FileHistoryEntry> Follow(this Repository repo, string path)
        {
            return new FileHistory(repo, path);
        }

        /// <summary>
        /// Gets the history of the file having the given <code>path</code> in the
        /// latest commits. The given <see cref="CommitFilter"/> instance specifies
        /// the commit sort strategy and range of commits to be considered.
        /// Only the time (corresponding to <code>--date-order</code>) and topological 
        /// (coresponding to <code>--topo-order</code>) sort strategies are supported.
        /// </summary>
        /// <param name="repo">The repository.</param>
        /// <param name="path">The file's path relative to the repository's root.</param>
        /// <param name="queryFilter">The filter to be used in querying the commit log.</param>
        /// <returns>The file's full history, including renames.</returns>
        /// <exception cref="ArgumentException">When an unsupported commit sort strategy is specified.</exception>
        /// <exception cref="ArgumentNullException">When any of the parameters is null.</exception>
        public static IEnumerable<FileHistoryEntry> Follow(this Repository repo, string path, CommitFilter queryFilter)
        {
            return new FileHistory(repo, path, queryFilter);
        }

        /// <summary>
        /// Filters the history of a file by excluding earlier periods during which
        /// the file had a different path.
        /// </summary>
        /// <param name="fileHistory">The file's history</param>
        /// <returns>The file's history excluding renames.</returns>
        public static IEnumerable<FileHistoryEntry> ExcludeRenames(this IEnumerable<FileHistoryEntry> fileHistory)
        {
            FileHistoryEntry lastReturnedEntry = null;
            foreach (FileHistoryEntry entry in fileHistory)
            {
                if (lastReturnedEntry != null && entry.Path != lastReturnedEntry.Path)
                {
                    yield break;
                }
                lastReturnedEntry = entry;
                yield return lastReturnedEntry;
            }
            yield break;
        }

        /// <summary>
        /// Gets collection of changed (i.e., new or modified) <see cref="Blob"/> instances,
        /// ignoring commits with which a file was only renamed.
        /// </summary>
        /// <returns>The collection of changed <see cref="Blob"/> instances.</returns>
        public static IEnumerable<Blob> ChangedBlobs(this IEnumerable<FileHistoryEntry> fileHistory)
        {
            Blob lastReturnedBlob = null;
            foreach (FileHistoryEntry entry in fileHistory)
            {
                Blob blob = entry.Commit.Tree[entry.Path].Target as Blob;
                if (blob != null && !blob.Equals(lastReturnedBlob))
                {
                    lastReturnedBlob = blob;
                    yield return lastReturnedBlob;
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Represents a file's history of commits in which that file was created,
    /// modified, or renamed.
    /// </summary>
    public class FileHistory : IEnumerable<FileHistoryEntry>
    {
        #region Properties

        /// <summary>
        /// The repository.
        /// </summary>
        protected readonly Repository repo;

        /// <summary>
        /// The file's path relative to the repository's root.
        /// </summary>
        protected readonly string path;

        /// <summary>
        /// The filter to be used in querying the commit log.
        /// </summary>
        protected readonly CommitFilter queryFilter;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FileHistory"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor is required in testing contexts.
        /// </remarks>
        protected FileHistory()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileHistory"/> class.
        /// The commits will be enumerated in reverse chronological order.
        /// </summary>
        /// <param name="repo">The repository.</param>
        /// <param name="path">The file's path relative to the repository's root.</param>
        /// <exception cref="ArgumentNullException">If any of the parameters is null.</exception>
        protected internal FileHistory(Repository repo, string path)
            : this(repo, path, new CommitFilter())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileHistory"/> class.
        /// The given <see cref="CommitFilter"/> instance specifies the commit
        /// sort strategies and range of commits to be considered.
        /// Only the time (corresponding to <code>--date-order</code>) and topological 
        /// (coresponding to <code>--topo-order</code>) sort strategies are supported.
        /// </summary>
        /// <param name="repo">The repository.</param>
        /// <param name="path">The file's path relative to the repository's root.</param>
        /// <param name="queryFilter">The filter to be used in querying the commit log.</param>
        /// <exception cref="ArgumentNullException">If any of the parameters is null.</exception>
        /// <exception cref="ArgumentException">When an unsupported commit sort strategy is specified.</exception>
        protected internal FileHistory(Repository repo, string path, CommitFilter queryFilter)
        {
            Ensure.ArgumentNotNull(repo, "repo");
            Ensure.ArgumentNotNull(path, "path");
            Ensure.ArgumentNotNull(queryFilter, "queryFilter");

            // Ensure the commit sort strategy makes sense.
            Ensure.ArgumentConformsTo<CommitFilter>(queryFilter, 
                filter => AllowedSortStrategies.Contains(filter.SortBy), "queryFilter");

            this.repo = repo;
            this.path = path;
            this.queryFilter = queryFilter;
        }

        #endregion

        #region IEnumerable<FileHistoryEntry> Members

        /// <summary>
        /// Gets the <see cref="IEnumerator{FileHistoryEntry}"/> that enumerates the
        /// <see cref="FileHistoryEntry"/> instances representing the file's history,
        /// including renames (as in <code>git log --follow</code>).
        /// </summary>
        /// <returns>A <see cref="IEnumerator{FileHistoryEntry}"/>.</returns>
        public virtual IEnumerator<FileHistoryEntry> GetEnumerator()
        {
            return FullHistory(this.repo, this.path, this.queryFilter).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region CommitFilter-Related Members

        /// <summary>
        /// The allowed commit sort strategies.
        /// </summary>
        protected static readonly List<CommitSortStrategies> AllowedSortStrategies = new List<CommitSortStrategies>
        {
            CommitSortStrategies.Topological, CommitSortStrategies.Time
        };

        /// <summary>
        /// Creates a <see cref="CommitFilter"/> from a base filter, setting <see cref="CommitFilter.SortBy"/>
        /// to <see cref="CommitSortStrategies.Time"/> and <see cref="CommitFilter.Since"/> to the given commit
        /// while retaining all other base filter attribute values.
        /// </summary>
        /// <param name="baseFilter">The base filter.</param>
        /// <param name="since">The first <see cref="Commit"/>.</param>
        /// <returns>A new instance of <see cref="CommitFilter"/>.</returns>
        protected CommitFilter GetCommitFilter(CommitFilter baseFilter, Commit since)
        {
            return new CommitFilter
            {
                SortBy = baseFilter.SortBy,
                FirstParentOnly = baseFilter.FirstParentOnly,
                Since = since,
                Until = baseFilter.Until
            };
        }

        #endregion

        #region History-Related Members

        /// <summary>
        /// Gets the relevant commits in which the given file was created, changed, or renamed.
        /// </summary>
        /// <param name="repo">The repository.</param>
        /// <param name="path">The file's path relative to the repository's root.</param>
        /// <param name="filter">The filter to be used in querying the commits log.</param>
        /// <returns>A collection of <see cref="FileHistoryEntry"/> instances.</returns>
        protected virtual IEnumerable<FileHistoryEntry> FullHistory(Repository repo, string path, CommitFilter filter)
        {
            // Return commits containing changes to the named file.
            Commit lastCommit = null;
            foreach (FileHistoryEntry entry in PartialHistory(repo, path, filter))
            {
                lastCommit = entry.Commit;
                yield return entry;
            }
            if (lastCommit == null)
            {
                yield break;
            }
            
            // Return further commits, following renames.
            Commit parentCommit = lastCommit.Parents.SingleOrDefault();
            if (parentCommit != null)
            {
                TreeChanges treeChanges = repo.Diff.Compare<TreeChanges>(parentCommit.Tree, lastCommit.Tree);
                TreeEntryChanges treeEntryChanges = treeChanges[path];
                if (treeEntryChanges != null && treeEntryChanges.Status == ChangeKind.Renamed)
                {
                    CommitFilter parentFilter = GetCommitFilter(filter, parentCommit);
                    string parentPath = treeEntryChanges.OldPath;
                    foreach (FileHistoryEntry entry in FullHistory(repo, parentPath, parentFilter))
                    {
                        yield return entry;
                    }
                }
            }
            yield break;
        }

        /// <summary>
        /// Gets the relevant commits in which the given file was created or changed.
        /// </summary>
        /// <param name="repo">The repository.</param>
        /// <param name="path">The file's path relative to the repository's root.</param>
        /// <param name="filter">The filter to be used in querying the commit log.</param>
        /// <returns>A collection of <see cref="FileHistoryEntry"/> instances.</returns>
        protected virtual IEnumerable<FileHistoryEntry> PartialHistory(Repository repo, string path, CommitFilter filter)
        {
            Func<Commit, bool> isRootCommit = c => c.Parents.Count() == 0;
            Func<Commit, bool> isMergeCommit = c => c.Parents.Count() > 1;
            Func<Commit, bool> isFileNewOrChanged = c => c.Parents.All(
                p => p.Tree[path] == null ||
                     p.Tree[path].Target.Id != c.Tree[path].Target.Id);

            return repo.Commits.QueryBy(filter)
                .TakeWhile(c => c.Tree[path] != null)
                .Where(c => isRootCommit(c) || (!isMergeCommit(c) && isFileNewOrChanged(c)))
                .Select(c => new FileHistoryEntry(path, c));
        }

        #endregion
    }

    /// <summary>
    /// An entry in a file's commit history.
    /// </summary>
    public class FileHistoryEntry
    {
        /// <summary>
        /// Creates a new instance of the <see cref="FileHistoryEntry"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor is required in testing contexts.
        /// </remarks>
        protected FileHistoryEntry()
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="FileHistoryEntry"/> class.
        /// </summary>
        /// <param name="path">The file's path.</param>
        /// <param name="commit">The commit in which the file was created, changed, or renamed.</param>
        protected internal FileHistoryEntry(string path, Commit commit)
        {
            Path = path;
            Commit = commit;
        }

        /// <summary>
        /// The file's path relative to the repository's root.
        /// </summary>
        public virtual string Path { get; internal set; }

        /// <summary>
        /// The commit in which the file was created or changed.
        /// </summary>
        public virtual Commit Commit { get; internal set; }
    }
}
