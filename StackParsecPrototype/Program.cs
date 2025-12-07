using StackParsecPrototype;
using static StackParsecPrototype.Module<LanguageExt.Common.Error, char>;
using static StackParsecPrototype.CharModule<LanguageExt.Common.Error>;

Span<byte> stackMem = stackalloc byte[1024];

var p1 = pure("testing").Map(s => s.Length);

var p2 = pure("testing").Bind(s => pure(s.Length));

var p3 = take(3);

var p4 = from x1 in take1
         from y1 in take1
         from z1 in take1
         from x2 in take1
         from y2 in take1
         from z2 in take1
         from x3 in take1
         from y3 in take1
         from z3 in take1
         select (x1, y1, z1, x2, y2, z2, x3, y3, z3);

var p5 = pure("testing")
          .Map(s => pure(s.Length))
          .Flatten();

var p6 = from x in tokens("abc").Map(x1 => x1.ToString())
         from y in tokens("xyz").Map(y1 => y1.ToString())
         from z in tokens("abc").Map(z1 => z1.ToString())
         select $"({x}, {y}, {z})";

 var p7 = from x in @string("abc")
          from y in @string("xyz")
          from z in @string("abc")
          select $"({x}, {y}, {z})";

var p8 = takeWhile(x => x is 'a' or 'b' or 'c');

var p9 = takeWhile1(x => x is 'x' or 'y' or 'z');

var p10 = from x in satisfy(Char.IsLetter)
          from y in satisfy(Char.IsLower)
          from z in satisfy(Char.IsLetterOrDigit)
          select $"({x}, {y}, {z})";

var r = p10.Parse("abcxyzabc", stackMem);

switch (r)
{
    case { Ok: true }:
        Console.WriteLine(r.Value);
        break;
    
    default:
        Console.WriteLine(string.Concat(r.ErrorDisplay));
        break;    
}

