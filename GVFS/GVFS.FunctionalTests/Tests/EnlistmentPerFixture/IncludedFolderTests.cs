﻿using GVFS.FunctionalTests.FileSystemRunners;
using GVFS.FunctionalTests.Should;
using GVFS.FunctionalTests.Tools;
using GVFS.Tests.Should;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace GVFS.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public class IncludedFolderTests : TestsWithEnlistmentPerFixture
    {
        private FileSystemRunner fileSystem = new SystemIORunner();
        private GVFSProcess gvfsProcess;
        private string mainIncludedFolder = Path.Combine("GVFS", "GVFS");
        private string[] allRootDirectories;
        private string[] directoriesInMainFolder;

        [OneTimeSetUp]
        public void Setup()
        {
            this.gvfsProcess = new GVFSProcess(this.Enlistment);
            this.allRootDirectories = Directory.GetDirectories(this.Enlistment.RepoRoot);
            this.directoriesInMainFolder = Directory.GetDirectories(Path.Combine(this.Enlistment.RepoRoot, this.mainIncludedFolder));
        }

        [TearDown]
        public void TearDown()
        {
            GitProcess.Invoke(this.Enlistment.RepoRoot, "clean -xdf");
            GitProcess.Invoke(this.Enlistment.RepoRoot, "reset --hard");

            foreach (string includedFolder in this.gvfsProcess.IncludedFoldersList())
            {
                this.gvfsProcess.RemoveIncludedFolders(includedFolder);
            }

            // Remove all included folders should make all folders appear again
            string[] directories = Directory.GetDirectories(this.Enlistment.RepoRoot);
            directories.ShouldMatchInOrder(this.allRootDirectories);
            this.ValidateIncludedFolders(new string[0]);
        }

        [TestCase, Order(1)]
        public void BasicTestsAddingAndRemoving()
        {
            this.gvfsProcess.AddIncludedFolders(this.mainIncludedFolder);
            this.ValidateIncludedFolders(this.mainIncludedFolder);

            string[] directories = Directory.GetDirectories(this.Enlistment.RepoRoot);
            directories.Length.ShouldEqual(2);
            directories[0].ShouldEqual(Path.Combine(this.Enlistment.RepoRoot, ".git"));
            directories[1].ShouldEqual(Path.Combine(this.Enlistment.RepoRoot, "GVFS"));

            string folder = this.Enlistment.GetVirtualPathTo(this.mainIncludedFolder);
            folder.ShouldBeADirectory(this.fileSystem);
            folder = this.Enlistment.GetVirtualPathTo(this.mainIncludedFolder, "CommandLine");
            folder.ShouldBeADirectory(this.fileSystem);

            folder = this.Enlistment.GetVirtualPathTo("Scripts");
            folder.ShouldNotExistOnDisk(this.fileSystem);
            folder = this.Enlistment.GetVirtualPathTo("GVFS", "GVFS.Common");
            folder.ShouldNotExistOnDisk(this.fileSystem);
        }

        [TestCase, Order(2)]
        public void AddingParentDirectoryShouldMakeItRecursive()
        {
            string childPath = Path.Combine(this.mainIncludedFolder, "CommandLine");
            this.gvfsProcess.AddIncludedFolders(childPath);
            string[] directories = Directory.GetDirectories(Path.Combine(this.Enlistment.RepoRoot, this.mainIncludedFolder));
            directories.Length.ShouldEqual(1);
            directories[0].ShouldEqual(Path.Combine(this.Enlistment.RepoRoot, childPath));
            this.ValidateIncludedFolders(childPath);

            this.gvfsProcess.AddIncludedFolders(this.mainIncludedFolder);
            directories = Directory.GetDirectories(Path.Combine(this.Enlistment.RepoRoot, this.mainIncludedFolder));
            directories.ShouldMatchInOrder(this.directoriesInMainFolder);
            this.ValidateIncludedFolders(childPath, this.mainIncludedFolder);
        }

        [TestCase, Order(3)]
        public void AddingSiblingFolderShouldNotMakeParentRecursive()
        {
            this.gvfsProcess.AddIncludedFolders(this.mainIncludedFolder);
            this.ValidateIncludedFolders(this.mainIncludedFolder);

            // Add and remove sibling folder to main folder
            string siblingPath = Path.Combine("GVFS", "FastFetch");
            this.gvfsProcess.AddIncludedFolders(siblingPath);
            string folder = this.Enlistment.GetVirtualPathTo(siblingPath);
            folder.ShouldBeADirectory(this.fileSystem);
            this.ValidateIncludedFolders(this.mainIncludedFolder, siblingPath);

            this.gvfsProcess.RemoveIncludedFolders(siblingPath);
            folder.ShouldNotExistOnDisk(this.fileSystem);
            folder = this.Enlistment.GetVirtualPathTo(this.mainIncludedFolder);
            folder.ShouldBeADirectory(this.fileSystem);
            this.ValidateIncludedFolders(this.mainIncludedFolder);
        }

        [TestCase, Order(4)]
        public void AddingSubfolderShouldKeepParentRecursive()
        {
            this.gvfsProcess.AddIncludedFolders(this.mainIncludedFolder);
            this.ValidateIncludedFolders(this.mainIncludedFolder);

            // Add subfolder of main folder and make sure it stays recursive
            string subFolder = Path.Combine(this.mainIncludedFolder, "Properties");
            this.gvfsProcess.AddIncludedFolders(subFolder);
            string folder = this.Enlistment.GetVirtualPathTo(subFolder);
            folder.ShouldBeADirectory(this.fileSystem);
            this.ValidateIncludedFolders(this.mainIncludedFolder, subFolder);

            folder = this.Enlistment.GetVirtualPathTo(this.mainIncludedFolder, "CommandLine");
            folder.ShouldBeADirectory(this.fileSystem);
        }

        [TestCase, Order(5)]
        public void CreatingFolderShouldAddToIncludedListAndStartProjecting()
        {
            this.gvfsProcess.AddIncludedFolders(this.mainIncludedFolder);
            this.ValidateIncludedFolders(this.mainIncludedFolder);

            string newFolderPath = Path.Combine(this.Enlistment.RepoRoot, "GVFS", "GVFS.Common");
            newFolderPath.ShouldNotExistOnDisk(this.fileSystem);
            Directory.CreateDirectory(newFolderPath);
            newFolderPath.ShouldBeADirectory(this.fileSystem);
            string[] fileSystemEntries = Directory.GetFileSystemEntries(newFolderPath);
            fileSystemEntries.Length.ShouldEqual(32);

            string projectedFolder = Path.Combine(newFolderPath, "Git");
            projectedFolder.ShouldBeADirectory(this.fileSystem);
            fileSystemEntries = Directory.GetFileSystemEntries(projectedFolder);
            fileSystemEntries.Length.ShouldEqual(13);

            string projectedFile = Path.Combine(newFolderPath, "ReturnCode.cs");
            projectedFile.ShouldBeAFile(this.fileSystem);
        }

        private void ValidateIncludedFolders(params string[] folders)
        {
            HashSet<string> actualIncludedFolders = new HashSet<string>(this.gvfsProcess.IncludedFoldersList());
            folders.Length.ShouldEqual(actualIncludedFolders.Count);
            foreach (string expectedFolder in folders)
            {
                actualIncludedFolders.Contains(expectedFolder).ShouldBeTrue($"{expectedFolder} not found in actual folder list");
            }
        }
    }
}