using System;
using System.Reflection;
using osu.Framework.Graphics.UserInterface;

class Program {
    static void Main() {
        var m = typeof(MenuItem).GetProperty("Action");
        Console.WriteLine(m != null);
    }
}
