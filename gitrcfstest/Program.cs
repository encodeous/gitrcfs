using System;
using System.Threading.Tasks;
using GitRCFS;

var repo = new FileRepository("https://github.com/encodeous/gitrcfstest", updateFrequencyMs: 5000);
var trackedFile = repo / "fgsdfgsdfg";
trackedFile.ContentsChanged += (value, newValue) =>
{
    Console.WriteLine(trackedFile);
};
await Task.Delay(-1);