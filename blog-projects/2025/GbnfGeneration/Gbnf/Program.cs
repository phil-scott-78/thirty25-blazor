using Gbnf;

MyApp.GetSimpleGbnf();
MyApp.GetSimpleJson();
var user = await ParseOrder.Parse(@"b:\models\Qwen2.5.1-Coder-1.5B-Instruct-Q8_0.gguf");

Console.WriteLine($"{user.FirstName} {user.LastName}, age {user.Age}. Is member? {user.IsMember}");