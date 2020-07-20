#addin "Cake.FileHelpers"
var target= Argument("target","MakeZip");
var configuration=Argument("configuration","Lab");
var configurationPlatform=Argument("configPlatform","Any CPU");
var solutionPath="../DemoApi.sln";
var binPath="../**/bin/"+configuration;
var objPath="../**/obj/"+configuration;
var testPath="../DemoApi.Tests/bin/"+configuration+"/DemoApi.Tests.dll";
if(configurationPlatform=="x64")
{
    testPath="../DemoApi.Tests/bin/x64/"+configuration+"/DemoApi.Tests.dll";
    binPath="../**/bin/x64/"+configuration;
    objPath="../**/obj/x64/"+configuration;
}
int taskCounter = 0;
int taskCount = 0;

Setup(context => {
            // declare recursive task count function
            Func<string, List<string>, int> countTask = null;
            countTask = (taskName, countedTasks) => {
                    if (string.IsNullOrEmpty(taskName) || countedTasks.Contains(taskName))
                    {
                        return 0;
                    }

                    countedTasks.Add(taskName);

                    var task = Tasks.Where(t=>t.Name == taskName).FirstOrDefault();
                    if (task == null)
                    {
                        return 0;
                    }

                    int result = 1;
                    countedTasks.Add(taskName);
                    foreach(var dependecy in task.Dependencies)
                    {
                        result+=countTask(dependecy.Name, countedTasks);
                    }

                    return result;
            };

        // count the task and store in globally available variable
        taskCount = countTask(target, new List<string>());
    });

TaskSetup(
    taskSetupContext => {
        ICakeTaskInfo task = taskSetupContext.Task;
        Information("Executing Task {0} of {1} (Name: {2}, Description: {3}, Dependencies: {4})",
            ++taskCounter,
            taskCount,
            task.Name,
            task.Description,
            string.Join(",",
                    task.Dependencies.Select(obj=>obj.Name).ToList()
                    )
            );
    });

Task("Clean")
.Description("Clean bin and obj folders of Projects and Test Projects")
.Does(()=>{
    CleanDirectories(binPath);
    CleanDirectories(objPath);
});
Task("Restore")
.Description("Restores the Nuget Packages")
.IsDependentOn("Clean")
.Does(()=>{
    NuGetRestore(solutionPath);
});
Task("Build")
.Description("Builds the solution")
.IsDependentOn("Restore")
.Does(()=>{
     var platform=PlatformTarget.MSIL;
     if(configurationPlatform=="x64")
     {
        platform=PlatformTarget.x64;
     }
     MSBuild(solutionPath,
        new MSBuildSettings()
        .SetConfiguration(configuration)
        .WithTarget("Build")
        .SetPlatformTarget(platform)
        .SetVerbosity(Verbosity.Minimal)
    );
});

Task("RunTests")
.Description("Runs MS Test")
.IsDependentOn("Build")
.Does(() =>{
    var testResultsFile = "../TestResults/test.txt";
    VSTest(testPath);
});

Task("MakeZip")
.Description("Bundle the deployed Zip")
.IsDependentOn("RunTests")
.Does(() =>{
    var platform=PlatformTarget.MSIL;
     if(configurationPlatform=="x64")
     {
        platform=PlatformTarget.x64;
     }
     MSBuild(solutionPath,
        new MSBuildSettings()
        .SetConfiguration(configuration)
        .SetPlatformTarget(platform)
        .WithProperty("DeployOnBuild","true")
        .WithProperty("WebPublishMethod","FileSystem")
        .WithProperty("DeployTarget","WebPublish")
        .WithProperty("publishUrl","./msbuild/")
        .SetVerbosity(Verbosity.Minimal)
    );
});

RunTarget(target);