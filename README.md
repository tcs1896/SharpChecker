## Student: Theodore Sill
## Advisor: Professor Matthew Fluet

###B. Thomas Golisano College of Computing and Information Sciences
###Rochester Institute of Technology
###Rochester, New York

###May 2017

The code in this repository was submitted in partial fulfillment of the requirements for 
the degree of Master of Science in Computer Science

#### Abstract

>Programming language type and runtime systems provide powerful guarantees about the
>behavior of a program when it is executed. However, they do not always ensure that a program
>will have the desired runtime characteristics or that the outcomes will align with the
>intent of the programmer. It is often necessary to provide additional assurances that a program
>is correct in this sense. Type annotation analysis frameworks provide mechanisms for
>ensuring correctness. They allow programmers to add additional information in the form
>of type annotations, and thus express their intent in such a way that it may be automatically
>verified.

>In the past, creating a type annotation analysis tool would be a large undertaking. Most
>compilers were black boxes which accepted source files as input and produced an intermediate
>representation or executable as output. Those wishing to add functionality might
>have liked to gain access to a program representation created by the compiler, such as an
>Abstract Syntax Tree (AST), but were forced to construct their own. Microsoft opened this
>black box when they delivered the .NET Compiler Platform (“Roslyn”), which exposes
>several APIs.

>In this work, we will explore these offerings and demonstrate that they may be leveraged
>to build a type annotation analysis system for C#. We call this tool “Sharp Checker”
>in homage to the Checker Framework which is a full featured solution for Java. The contribution
>of this work is to translate the mechanisms of annotation processing at work in
>tools like the Checker Framework to the Visual Studio IDE. Sharp Checker is an extensible
>framework which gives feedback within Visual Studio as the user types and upon compilation.
>We have demonstrated Sharp Checker’s usefulness by implementing the Encrypted,
>Nullness, and Tainted annotated type systems and applying them to the Sharp Checker
>source itself, in addition to several applications publicly available on GitHub.

For more information about the content of this repository please see Repository ReadMe.pdf
