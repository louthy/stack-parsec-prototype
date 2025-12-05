using StackParsecPrototype;
using static StackParsecPrototype.Parsec<LanguageExt.Common.Error, char>;

Span<byte> stackMem = stackalloc byte[1024];

//var p1 = pure("testing").Map(s => s.Length);
//var p2 = pure("testing").Bind(s => pure(s.Length));
//var p3 = take(3);
 var p4 = from x in take1
          from y in take1
          from z in take1
          select (x, y, z);

/*var p5 = pure("testing")
            .Map(s => pure(s.Length))
            .Flatten();*/

var r = p4.Parse("abc".AsSpan(), stackMem);

var rv = r.Value;

Console.WriteLine("Hello, World!");
