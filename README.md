# stack-parsec-prototype
Attempt to build monadic parser combinators using just stack primitives (ref structs)

**NOT FOR HUMAN CONSUMPTION**

This project is not maintained, it is a prototyping project only. It may have some areas of insight or equally it 
may have none, it may work, it may not.  

## Concept

Parser combinators in C# that leverage LINQ (like [Parsec](https://github.com/louthy/language-ext/tree/main/LanguageExt.Parsec) and [Megaparsec](https://github.com/louthy/language-ext/blob/v5-megaparsec/LanguageExt.Megaparsec) in [language-ext](https://github.com/louthy/language-ext)) need to create lots of 
temporary heap-resident objects. This is an attempt to see how far I can push the `ref struct` idea to create 
stack-resident parser-combinators.

_There's no way to remove all allocations (and keep the pure functional style and LINQ operators), but it should be
possible to massively reduce the allocations for a certain subset of parser capabilities._

One early realisation is that to make something like this work, I have had to effectively rebuild core ideas from
the .NET runtime (stacks, object reference tracking, byte-code instruction set, etc.) -- this tells me that it's
probably better to let the highly optimised .NET runtime do what it's good at (rather than create a poor man's 
version of it).

However, and why I'm still interested in this idea, I think for small parsers there really is a way to do pretty
much everything on the stack. This may well even compose to bigger parsers, but I think inevitably we have to do
some allocations on-the-fly, but with small parsers it could be both efficient enough and allocation free.

TBC.

## Examples

```c#

var p0 = pure(1) | pure(2) | pure(3);

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

var p6 = from x in asString(tokens("abc"))
         from y in asString(tokens("xyz"))
         from z in asString(tokens("abc"))
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

var p11 = from x in oneOf(['a', 'b', 'c'])
          from y in oneOf(['a', 'b', 'c'])
          from z in oneOf(['a', 'b', 'c'])
          from _ in oneOf(['a', 'b', 'c'])
          select $"({x}, {y}, {z})";

var p12 = oneOf(['a', 'b', 'c']) | oneOf(['x', 'y', 'z']);
```
