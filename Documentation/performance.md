# Performance

Speed is one of the main concerns of machine learning algorithms developers. The native SVM 
implementation (LIBSVM 3.20) is optimized for specific kernel types and demonstrates good training and test times for small and large problems. The F# implementation running on Mono 5.10 is 5-6 times slower than the native SVM that makes it unsuitable for large problems. However, with parallel kernel evaluations the F# implementation is only two times slower that makes it practically useful.

## MacOS X

 * **Computer**: MacBook Pro (Model A1398)
 * **Processor**: 2.2 GHz Intel Core i7
 * **Memory**: 16 GB 1600 MHz DDR3
 * **Operating System**: OS X High Sierra (10.13.4) 
 * **.NET Framework**: Mono 5.10.1.47

### Adult Dataset
* Features: 123
* Kernel: RBF $\gamma=1$
* Cost: C=1

 <table>
    <thead>
        <tr>
            <th rowspan="3">Dataset</th>
        </tr>
        <tr>
            <th colspan="4">LIBSVM 3.20</th>
            <th colspan="5">Semagle</th>
        </tr>
        <tr>
            <!-- LIBSVM -->
            <th>#</th>
            <th>Train</th>
            <th>Test</th>
            <th>Accuracy</th>
            <!-- Semagle -->
            <th>#</th>
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
            <td>1584</td>
            <td>0.24</td>
            <td>3.68</td>
            <td>76.29%</td>
            <!-- Semagle -->
            <td>1582</td>
            <td>1.56</td>
            <td>0.95</td>
            <td>10.03</td>
            <td>76.29%</td>
        </tr>
        <tr>
            <td>a2a&nbsp;(2,265/30,296)</td>
            <!-- LIBSVM -->
            <td>2233</td>
            <td>0.47</td>
            <td>4.98</td>
            <td>76.51%</td>
            <!-- Semagle -->
            <td>2227</td>
            <td>3.06</td>
            <td>1.80</td>
            <td>30.40</td>
            <td>76.52%</td>
        </tr>
        <tr>
            <td>a3a&nbsp;(3,185/29,376)</td>
            <!-- LIBSVM -->
            <td>3103</td>
            <td>0.91</td>
            <td>6.68</td>
            <td>76.73%</td>
            <!-- Semagle -->
            <td>3098</td>
            <td>5.95</td>
            <td>3.16</td>
            <td>40.93</td>
            <td>76.73%</td>
        </tr>
        <tr>
            <td>a4a&nbsp;(4,781/27,780)</td>
            <!-- LIBSVM -->
            <td>4595</td>
            <td>1.99</td>
            <td>9.30</td>
            <td>77.73%</td>
            <!-- Semagle -->
            <td>4592</td>
            <td>12.97</td>
            <td>6.62</td>
            <td>56.20</td>
            <td>77.72%</td>
        </tr>
        <tr>
            <td>a5a&nbsp;(6,414/26,147)</td>
            <!-- LIBSVM -->
            <td>6072</td>
            <td>5.84</td>
            <td>11.64</td>
            <td>77.89%</td>
            <!-- Semagle -->
            <td>6071</td>
            <td>41.45</td>
            <td>17.21</td>
            <td>69.99</td>
            <td>77.89%</td>
        </tr>
        <tr>
            <td>a6a&nbsp;(11,220/21,341)</td>
            <!-- LIBSVM -->
            <td>10235</td>
            <td>27.58</td>
            <td>15.80</td>
            <td>78.53%</td>
            <!-- Semagle -->
            <td>10226</td>
            <td>191.81</td>
            <td>70.45</td>
            <td>98.15</td>
            <td>78.53%</td>
        </tr>
        <tr>
            <td>a7a&nbsp;(16,100/16,461)</td>
            <!-- LIBSVM -->
            <td>14275</td>
            <td>56.98</td>
            <td>16.82</td>
            <td>79.66%</td>
            <!-- Semagle -->
            <td>14253</td>
            <td>395.36</td>
            <td>129.13</td>
            <td>105.64</td>
            <td>79.66%</td>
        </tr>
        <tr>
            <td>a8a&nbsp;(22,696/9,865)</td>
            <!-- LIBSVM -->
            <td>19487</td>
            <td>116.17</td>
            <td>14.13</td>
            <td>80.31%</td>
            <!-- Semagle -->
            <td>19479</td>
            <td>765.86</td>
            <td>248.76</td>
            <td>91.87</td>
            <td>80.31%</td>
        </tr>
        <tr>
            <td>a9a&nbsp;(32,561/16,281)</td>
            <!-- LIBSVM -->
            <td>26975</td>
            <td>229.35</td>
            <td>32.58</td>
            <td>80.77%</td>
            <!-- Semagle -->
            <td>26952</td>
            <td>1564.13</td>
            <td>468.29</td>
            <td>222.60</td>
            <td>80.77%</td>
        </tr>
    </tbody>
 </table>

### Web Dataset
* Features: 300
* Kernel: RBF $\gamma=1$
* Cost: C=1

 <table>
    <thead>
        <tr>
            <th rowspan="3">Dataset</th>
        </tr>
        <tr>
            <th colspan="4">LIBSVM 3.20</th>
            <th colspan="5">Semagle</th>
        </tr>
        <tr>
            <!-- LIBSVM -->
            <th>#</th>
            <th>Train</th>
            <th>Test</th>
            <th>Accuracy</th>
            <!-- Semagle -->
            <th>#</th>
            <th>Train (seq.)</th>
            <th>Train (par.)</th>
            <th>Test</th>
            <th>Accuracy</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>w1a&nbsp;(2,477/47,272)</td>
            <!-- LIBSVM -->
            <td>2080</td>
            <td>0.49</td>
            <td>8.26</td>
            <td>97.30%</td>
            <!-- Semagle -->
            <td>2080</td>
            <td>2.78</td>
            <td>1.56</td>
            <td>39.09</td>
            <td>97.30%</td>
        </tr>
        <tr>
            <td>w2a&nbsp;(3,470/46,279)</td>
            <!-- LIBSVM -->
            <td>2892</td>
            <td>0.97</td>
            <td>11.26</td>
            <td>97.35%</td>
            <!-- Semagle -->
            <td>2889</td>
            <td>5.42</td>
            <td>2.81</td>
            <td>52.43</td>
            <td>97.35%</td>
        </tr>
        <tr>
            <td>w3a&nbsp;(4,912/44,837)</td>
            <!-- LIBSVM -->
            <td>4018</td>
            <td>1.89</td>
            <td>15.24</td>
            <td>97.37%</td>
            <!-- Semagle -->
            <td>4017</td>
            <td>10.15</td>
            <td>4.77</td>
            <td>71.60</td>
            <td>97.37%</td>
        </tr>
        <tr>
            <td>w4a&nbsp;(7,366/42,383)</td>
            <!-- LIBSVM -->
            <td>5886</td>
            <td>6.57</td>
            <td>20.84</td>
            <td>97.43%</td>
            <!-- Semagle -->
            <td>5886</td>
            <td>31.93</td>
            <td>13.04</td>
            <td>99.66</td>
            <td>97.43%</td>
        </tr>
        <tr>
            <td>w5a&nbsp;(9,888/39,861)</td>
            <!-- LIBSVM -->
            <td>7715</td>
            <td>13.69</td>
            <td>25.42</td>
            <td>97.45%</td>
            <!-- Semagle -->
            <td>7706</td>
            <td>71.44</td>
            <td>27.67</td>
            <td>126.60</td>
            <td>97.45%</td>
        </tr>
        <tr>
            <td>w6a&nbsp;(17,188/32,561)</td>
            <!-- LIBSVM -->
            <td>12864</td>
            <td>56.68</td>
            <td>34.65</td>
            <td>97.60%</td>
            <!-- Semagle -->
            <td>12855</td>
            <td>324.51</td>
            <td>120.14</td>
            <td>177.09</td>
            <td>97.60%</td>
        </tr>
        <tr>
            <td>w7a&nbsp;(24,692/25,057)</td>
            <!-- LIBSVM -->
            <td>17786</td>
            <td>97.58</td>
            <td>38.73</td>
            <td>97.61%</td>
            <!-- Semagle -->
            <td>17777</td>
            <td>558.34</td>
            <td>175.71</td>
            <td>185.19</td>
            <td>97.61%</td>
        </tr>
        <tr>
            <td>w8a&nbsp;(49,749/14,951)</td>
            <!-- LIBSVM -->
            <td>32705</td>
            <td>375.60</td>
            <td>42.58</td>
            <td>99.39%</td>
            <!-- Semagle -->
            <td>32678</td>
            <td>2227.85</td>
            <td>691.96</td>
            <td>229.72</td>
            <td>99.39%</td>
        </tr>
    </tbody>
 </table>

