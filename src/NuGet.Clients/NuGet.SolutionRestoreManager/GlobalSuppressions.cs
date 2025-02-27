// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "https://github.com/NuGet/Home/issues/7674", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.RestoreEventPublisher.OnSolutionRestoreCompleted(NuGet.VisualStudio.SolutionRestoredEventArgs)")]
[assembly: SuppressMessage("Performance", "VSSDK004:Use BackgroundLoad flag in ProvideAutoLoad attribute for asynchronous auto load.", Justification = "https://github.com/NuGet/Home/issues/8796", Scope = "type", Target = "~T:NuGet.SolutionRestoreManager.RestoreManagerPackage")]
[assembly: SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "https://github.com/microsoft/vs-threading/issues/577", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.SolutionRestoreJob.CheckPackagesConfigAsync~System.Threading.Tasks.Task{System.Boolean}")]
[assembly: SuppressMessage("Usage", "VSTHRD108:Assert thread affinity unconditionally", Justification = "Unclear what the consequences when the dispose is called from the analyzer", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.RestoreManagerPackage.Dispose(System.Boolean)")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'OnSolutionRestoreCompleted' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.RestoreEventPublisher.OnSolutionRestoreCompleted(NuGet.VisualStudio.SolutionRestoredEventArgs)")]
[assembly: SuppressMessage("Build", "CA1303:Method 'Task SolutionRestoreWorker.PromoteTaskToActiveAsync(BackgroundRestoreOperation restoreOperation, CancellationToken token)' passes a literal string as parameter 'message' of a call to 'InvalidOperationException.InvalidOperationException(string message)'. Retrieve the following string(s) from a resource table instead: \"Failed promoting pending task.\".", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.SolutionRestoreWorker.PromoteTaskToActiveAsync(NuGet.SolutionRestoreManager.SolutionRestoreWorker.BackgroundRestoreOperation,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'ScheduleRestoreAsync' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.SolutionRestoreWorker.ScheduleRestoreAsync(NuGet.VisualStudio.SolutionRestoreRequest,System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Boolean}")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'StartBackgroundJobRunnerAsync' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.SolutionRestoreWorker.StartBackgroundJobRunnerAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Boolean}")]
