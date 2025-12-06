# stack-parsec-prototype
Attempt to build monadic parser combinators using just stack primitives (ref structs)

**NOT FOR HUMAN CONSUMPTION**

This project is not maintained, it is a prototyping project only. It may have some areas of insight or equally it 
may have none, it may work, it may not.  

## Concept

Parser combinators in C# that leverage LINQ ([like Parsec and Megaparsec in language-ext](https://github.com/louthy/language-ext)) need to create lots of 
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
