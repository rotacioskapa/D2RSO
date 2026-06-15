using D2SLib;
using D2SLib.Model.Save;

D2S character = D2S.Read(File.ReadAllBytes("C:\\Users\\ShadowEvil\\Downloads\\Amazon.d2s"));

Console.WriteLine(character.Name);

Console.ReadKey();