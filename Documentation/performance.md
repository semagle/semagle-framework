# Performance

Performance is one of the main concerns of machine learning algorithms developers. The native SVM 
implementation highly optimized for specific kernel types  (LIBSVM) definitely shows the best training 
and testing times. Yet on Windows, LIBSVM is it is just 3 times faster than the general F# implementation 
that runs on Microsoft .Net Framework 4.6.1. On MacOS X and Mono 4.4.2 the performance of the F# code is 
more than 10 times worse that the native code, but on .NET Core 1.0.1 it is similar to Windows. Thus, 
the tradeoff between productivity and performance is fair enough.

## Windows

 * **Computer**: Dell Inspirion 5521
 * **Processor**: 2.0 GHz Intel Core i7
 * **Memory**: 8 GB
 * **Operating System**: Windows 10 64-bit
 * **.NET Framework**: Microsoft .NET Framework 4.6.1


 <table border="1" cellpadding="5">
    <thead>
        <tr>
            <th rowspan="3">Time<br>(seconds)</th>
            <th colspan="3">Executables</th>
        </tr>
        <tr>
            <th rowspan="2">LIBSVM 3.20<br>Native</th>
            <th colspan="2">Microsoft .NET 4.6.1 Runtime</th>
        </tr>
        <tr>
            <th>Mono 4.4.2</th>
            <th>MS .NET 4.6.1</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>train</td>
            <td>83.937</td>
            <td>268.364</td>
            <td>260.278</td>
        </tr>
        <tr>
            <td>test</td>
            <td>18.945</td>
            <td>39.990</td>
            <td>38.854</td>
        </tr>
    </tbody>
 </table>

## MacOS X

 * **Computer**: MacBook Pro (Retina, 15-inch, Mid 2014)
 * **Processor**: 2.2 GHz Intel Core i7
 * **Memory**: 16 GB 1600 MHz DDR3
 * **Operating System**: OS X El Capitan (10.11.6)
 * **.NET Framework**: Mono 4.4.2 / .NET Core 1.0.1


 <table border="1" cellpadding="5">
    <thead>
        <tr>
            <th rowspan="3">Time<br>(seconds)</th>
            <th colspan="5">Executables</th>
        </tr>
        <tr>
            <th rowspan="2">LIBSVM 3.20<br>Native</th>
            <th colspan="2">Mono 4.4.2 Runtime</th>
            <th colspan="2">.NET Core 1.0.1</th>
        </tr>
        <tr>
            <th>Mono 4.4.2</th>
            <th>MS .NET 4.6.1</th>
            <th>Mono 4.4.2</th>
            <th>MS .NET 4.6.1</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>train</td>
            <td>56.497</td>
            <td>702.595</td>
            <td>728.337</td>
            <td>234.270</td>
            <td>236.324</td>
        </tr>
        <tr>
            <td>test</td>
            <td>12.706</td>
            <td>88.464</td>
            <td>102.674</td>
            <td>36.199</td>
            <td>29.427</td>
        </tr>
    </tbody>
 </table>
