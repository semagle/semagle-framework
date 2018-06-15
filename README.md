# Semagle: F# Framework for Machine Learning and Natural Language Processing

Semagle F# Framework is a demonstration of the idea that machine learning and natural language processing 
problems can be productively solved using F# language. At the current stage, it is hard to achieve even the 
2x performance of the C code, but .NET runtime and F# compiler are improving. Thus, we might expect 
that the performance will improve over the time. 

## Building

- Simply build Semagle.Framework.sln in Visual Studio 2015, Mono Develop, or Xamarin Studio, or use the FAKE build:
 * [![AppVeyor build status](https://ci.appveyor.com/api/projects/status/nik2nx0m9kh3a84f?svg=true)](https://ci.appveyor.com/project/sslipchenko/semagle-framework/)
   Windows: Run *build.cmd*
 * [![Travis build status](https://travis-ci.org/sslipchenko/semagle-framework.svg)](https://travis-ci.org/sslipchenko/semagle-framework)
   Mono on Linux or MacOS X: Run *build.sh*

## Documentation

See [Semagle.Framework](https://semagle.github.io/semagle-framework/) documentation pages. 

## License

The library is available under Apache 2.0. For more information see the [License file][1] in the GitHub repository.

## Maintainers

The maintainer of this project is [Serge Slipchenko](http://github.com/sslipchenko)

 [1]: https://github.com/semagle/semagle-framework/blob/master/LICENSE.md
