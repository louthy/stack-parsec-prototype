using StackParsecPrototype;
using static StackParsecPrototype.Parsec<LanguageExt.Common.Error, char>;

Span<byte> stackMem = stackalloc byte[1024];

//var p1 = pure("testing").Map(s => s.Length);
//var p2 = pure("testing").Bind(s => pure(s.Length));
//var p3 = take(3);
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

/*var p5 = pure("testing")
            .Map(s => pure(s.Length))
            .Flatten();*/

var r = p4.Parse("abcabcabc".AsSpan(), stackMem);

switch (r)
{
    case { Ok: true }:
        Console.WriteLine(r.Value);
        break;
    
    default:
        Console.WriteLine(r.Errors.ToString());
        break;    
}

