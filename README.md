# GitRCFS - Git Remote Config Filesystem

## `What is GitRCFS?`

`GitRCFS` offers a lightweight option to synchronize an application's configuration & data files with a remote git repository. This library provides a simple api to use a git repository to access readonly configuration files.

## `Why is this useful?`

There are 2 main benefits to using remote git repositories for configuration file hosting.

1. `GitRCFS` enables configuration files to be synced across numerous different servers / devices. This saves the hassle of updating configuration files manually, and offers a single place to manage config files.
2. Another benefit is due to using Git as a version control, as a result, `GitRCFS` inherits all the benefits of Git. This way, config files could be shared across teams, rolled back, and be used in a much more flexible way.

## `Using GitRCFS`

The GitRCFS library is extremely simple to use.

First create a `FileRepository`,

```csharp
var repo = new FileRepository("https://github.com/encodeous/gitrcfstest",
    branch: "main", accessToken: "<your-token>", updateFrequencyMs: 5000);
```

You can optionally specify an access token, the branch to access the configuration files, and how frequently to check for configuration updates.

To access a file in the filesystem, there are 2 main ways:

1. Use the `/` operator to combine a path
   ```c#
   var configFile = repo / "config-dir" / "config.json";
   ```
2. Use the `[]` operator to select a path
    ```c#
   var configFile = repo["config-dir/config.json"];
   ```

All files and directories are of the type `RcfsNode`

Through this type, it is possible to listen for file changes, iterate the children of directories, and get the data from files.

## `Samples`

Here are some code snippets showing the usage of `GitRCFS`.

### `Read data from repo`
```c#
// simple use case to read data from a repo:
var repo = new FileRepository("https://github.com/encodeous/gitrcfstest");
Console.WriteLine($"File content from repo: \"{repo["folder/file-in-folder.txt"].GetStringData()}\"");
```

### `Reading serialized data from repo`
```c#
// deserializing a file into a C# class
var repo = new FileRepository("https://github.com/encodeous/gitrcfstest");
Console.WriteLine($"Time is {repo["serialized.json"].DeserializeData<DateTime>()}");
```

### `Monitoring repo for changes`
```c#
// monitor for changes
var repo = new FileRepository("https://github.com/encodeous/gitrcfstest");
var file = repo["folder/file-in-folder.txt"];
Console.WriteLine($"Initial file content from repo: \"{file.GetStringData()}\"");
// file changed event handler
file.ContentsChanged += (_,_) =>
{
    Console.WriteLine($"New file content: \"{file.GetStringData()}\"");
};
```

### `Authenticating`

```c#
// simple use case to read data from a repo when logged in:
var repo = new FileRepository("https://github.com/encodeous/gitrcfstest", username: "user", password: "password");
Console.WriteLine($"File content from repo: \"{repo["folder/file-in-folder.txt"].GetStringData()}\"");
```