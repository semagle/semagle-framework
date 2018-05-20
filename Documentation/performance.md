# Performance

Computational performance is one of the main concerns of machine learning algorithms developers. The native SVM 
implementation (LIBSVM 3.20) is optimized for specific kernel types and demonstrates good training and test times for small and large problems. The F# implementation is slower for small problems, but starting from 10,000 training samples its training time is similar to the native implementation. Moreover, the F# implementation with parallel kernel evalations is two times faster than the native implementation.

## MacOS X

 * **Computer**: MacBook Pro (Model A1398)
 * **Processor**: 2.2 GHz Intel Core i7
 * **Memory**: 16 GB 1600 MHz DDR3
 * **Operating System**: OS X High Sierra (10.13.4) 
 * **.NET Framework**: Mono 5.10.1.47

 <table>
    <thead>
        <tr>
            <th rowspan="3">Dataset</th>
        </tr>
        <tr>
            <th colspan="3">LIBSVM 3.20</th>
            <th colspan="4">Semagle</th>
        </tr>
        <tr>
            <!-- LIBSVM -->
            <th>Train</th>
            <th>Test</th>
            <th>Accuracy</th>
            <!-- Semagle -->
            <th>Train (seq.)</th>
            <th>Train (par.)</th>
            <th>Test</th>
            <th>Accuracy</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>a1a&nbsp;(1,605/30,956)</td>
            <!-- LIBSVM -->
            <td>0.24</td>
            <td>3.68</td>
            <td>76.29%</td>
            <!-- Semagle -->
            <td>0.73</td>
            <td>0.49</td>
            <td>10.03</td>
            <td>84.20%</td>
        </tr>
        <tr>
            <td>a2a&nbsp;(2,265/30,296)</td>
            <!-- LIBSVM -->
            <td>0.47</td>
            <td>4.98</td>
            <td>76.51%</td>
            <!-- Semagle -->
            <td>1.43</td>
            <td>0.70</td>
            <td>14.34</td>
            <td>84.17%</td>
        </tr>
        <tr>
            <td>a3a&nbsp;(3,185/29,376)</td>
            <!-- LIBSVM -->
            <td>0.91</td>
            <td>6.68</td>
            <td>76.73%</td>
            <!-- Semagle -->
            <td>2.58</td>
            <td>1.13</td>
            <td>17.66</td>
            <td>84.18%</td>
        </tr>
        <tr>
            <td>a4a&nbsp;(4,781/27,780)</td>
            <!-- LIBSVM -->
            <td>1.99</td>
            <td>9.30</td>
            <td>77.73%</td>
            <!-- Semagle -->
            <td>2.18</td>
            <td>5.37</td>
            <td>24.51</td>
            <td>84.38%</td>
        </tr>
        <tr>
            <td>a5a&nbsp;(6,414/26,147)</td>
            <!-- LIBSVM -->
            <td>5.84</td>
            <td>11.64</td>
            <td>77.89%</td>
            <!-- Semagle -->
            <td>9.30</td>
            <td>3.94</td>
            <td>30.61</td>
            <td>84.57%</td>
        </tr>
        <tr>
            <td>a6a&nbsp;(11,220/21,341)</td>
            <!-- LIBSVM -->
            <td>27.58</td>
            <td>15.80</td>
            <td>78.53%</td>
            <!-- Semagle -->
            <td>27.63</td>
            <td>10.41</td>
            <td>43.02</td>
            <td>84.64%</td>
        </tr>
        <tr>
            <td>a7a&nbsp;(16,100/16,461)</td>
            <!-- LIBSVM -->
            <td>56.98</td>
            <td>16.82</td>
            <td>79.66%</td>
            <!-- Semagle -->
            <td>60.12</td>
            <td>22.91</td>
            <td>47.52</td>
            <td>84.68%</td>
        </tr>
        <tr>
            <td>a8a&nbsp;(22,696/9,865)</td>
            <!-- LIBSVM -->
            <td>116.17</td>
            <td>14.13</td>
            <td>80.31%</td>
            <!-- Semagle -->
            <td>120.30</td>
            <td>41.39</td>
            <td>44.72</td>
            <td>85.40%</td>
        </tr>
        <tr>
            <td>a9a&nbsp;(32,561/16,281)</td>
            <!-- LIBSVM -->
            <td>229.35</td>
            <td>32.58</td>
            <td>80.77%</td>
            <!-- Semagle -->
            <td>337.87</td>
            <td>98.75</td>
            <td>95.38</td>
            <td>85.03%</td>
        </tr>
    </tbody>
 </table>
