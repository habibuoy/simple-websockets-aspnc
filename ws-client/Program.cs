// See https://aka.ms/new-console-template for more information
Main:
Console.Clear();
Console.WriteLine("Hello, World! Type exit to quit.");
Console.Write("Input: ");

var line = Console.ReadLine();
if (line == "exit")
{
    return;
}
else
{
    goto Main;
}