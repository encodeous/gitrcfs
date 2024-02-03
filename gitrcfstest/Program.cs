using System;
using System.Text.Json;
using System.Threading.Tasks;
using GitRCFS;

// sample 1
{
    // simple use case to read data from a repo:
    var repo = new FileRepository("https://github.com/encodeous/gitrcfstest");
    Console.WriteLine($"File content from repo: \"{repo["folder/file-in-folder.txt"].GetStringData()}\"");
}
// sample 2
{
    // deserializing a file into a C# class
    var repo = new FileRepository("https://github.com/encodeous/gitrcfstest");
    Console.WriteLine($"Time is {repo["serialized.json"].DeserializeData<DateTime>()}");
}
// sample 3
{
    // monitor for changes
    var repo = new FileRepository("https://github.com/encodeous/gitrcfstest");
    var file = repo["folder/file-in-folder.txt"];
    Console.WriteLine($"Initial file content from repo: \"{file.GetStringData()}\"");
    // file changed event handler
    file.ContentsChanged += (_,_) =>
    {
        Console.WriteLine($"New file content: \"{file.GetStringData()}\"");
    };
}
// sample 4
{
    // simple use case to read data from a repo when logged in:
    var repo = new FileRepository("https://github.com/encodeous/gitrcfstest", username: "user", password: "password");
    Console.WriteLine($"File content from repo: \"{repo["folder/file-in-folder.txt"].GetStringData()}\"");
}