## Student: Theodore Sill
## Advisor: Professor Matthew Fluet

### B. Thomas Golisano College of Computing and Information Sciences
### Rochester Institute of Technology
### Rochester, New York

### May 2017

The code in this repository was submitted in partial fulfillment of the requirements for 
the degree of Master of Science in Computer Science

#### Abstract

>Programming language type and runtime systems provide powerful guarantees about the
>behavior of a program when it is executed. However, they do not always ensure that a
>program will have the desired runtime characteristics, or that the outcomes will align with
>the intent of the programmer. It is often necessary to provide additional assurances that a
>program is correct in this sense. Type annotation analysis frameworks are static analysis
>tools that allow programmers to add additional information in the form of type annotations,
>and thus express their intent in such a way that it may be automatically verified.
>
>In the past, creating a type annotation analysis tool would have been a large undertaking.
>Most compilers were black boxes which accepted source files as input and produced an
>executable as output. Those wishing to make use of a program representation, such as
>an Abstract Syntax Tree (AST), were forced to construct their own. Microsoft opened this
>black box when they delivered the .NET Compiler Platform (code named “Roslyn”), which
>exposes several APIs.
>
>In this work, we will explore these offerings and describe how they were leveraged to
>build a type annotation analysis tool for C#. We call this tool “Sharp Checker” in homage
>to the Checker Framework, which is a full-featured solution for Java. The contribution of
>this work is to translate the mechanisms of annotation processing at work in tools like the
>Checker Framework to the Visual Studio IDE, where users receive feedback immediately
>as they type and upon compilation. Sharp Checker may be installed as a NuGet package,
>and the source code is available on GitHub. We have demonstrated Sharp Checker’s extensibility
>and usefulness by implementing the Encrypted, Nullness, and Tainted annotated
>type systems and applying them to the Sharp Checker source code, as well as publicly available
>applications. In the process, we discovered and corrected several bugs, while gaining
>insights into the properties which may be enforced by type annotation analysis.

For more information about the content of this repository please see Repository ReadMe.pdf
