# Semagle: F# Framework for Machine Learning and Natural Language Processing

Semagle F# Framework is a demonstration of the idea that machine learning and natural language processing 
problems can be productively solved using F# language. At the current stage, it is hard to achieve even the 
2x performance of the C code, but .NET runtime and F# compiler are improving.

## Building

- Simply build Semagle.Framework.sln in Visual Studio 2015, Mono Develop, or Xamarin Studio, or use the FAKE build:
 * Windows: Run *build.cmd*
 * Mono on Linux or MacOS X: Run *build.sh*  

## Tutorials and documentation

### Tutorials

The following tutorials contain examples that demonstrate features of Semagle.Framework:

 * [SVM classification and regression](tutorials/SVM.html) - shows how to train and evaluate the performance of 
   SVM models for classification and regression problems.

### Reference documentation

There's also [reference documentation](reference/index.html) available. 

## License

Copyright 2016-2018 Serge Slipchenko (Serge.Slipchenko@gmail.com)

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed 
on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for 
the specific language governing permissions and limitations under the License.
