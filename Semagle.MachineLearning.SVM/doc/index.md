# Semagle.MachineLearning.SVM Library

Semagle.MachineLearning.SVM implements training and prediction functions for two class classification, 
one class classification and regression. The library is based on generalization of Sequential Minimal 
Optimization algorithm.

## Kernels

The library implements the following popular kernels:

 * `Kernel.linear` - $\mathbf{x}_i \cdot \mathbf{x}_j$
 * `Kernel.polynomial` - $(\gamma(\mathbf{x}_i \cdot \mathbf{x}_j) + \mu)^n$
 * `Kernel.rbf` - $e^{\gamma(\mathbf{x}_i - \mathbf{x}_j)^2}$
 * `Kernel.sigmoid` - $tanh(\gamma(\mathbf{x}_i \cdot \mathbf{x}_j) + \mu)$

## Two Class Classification

### Training
Function `SMO.C_SVC` finds a solution of the following optimization problem:
$$
\begin{array}
	\mathop{min}_{\mathbf{w},b,\mathbf{\xi}} & \quad \frac{1}{2} \mathbf{w}^T\mathbf{w} + C \sum_{i=1}^l \xi_i \\
    \text{subject to} & \quad y_i(\mathbf{w}^T\phi(\mathbf{x}_i) + b) \geq 1 - \xi_i \\
	& \quad \xi_i \geq 0, i=1, \dots, l
\end{array}
$$
in the dual form:
$$
\begin{array}
	\mathop{min}_{\mathbf{\alpha}} & \quad \frac{1}{2} \mathbf{\alpha}^TQ\mathbf{w} - \mathbf{e}^T\mathbf{\alpha} \\
    \text{subject to} & \quad \mathbf{y}^T\mathbf{\alpha} = 0 \\
	& \quad 0 \leq \alpha_i \leq C, i=1, \dots, l
\end{array}
$$
where $Q_{ij}=y_iy_jK(\mathbf{x}_i, \mathbf{x}_j)$.

    open Semagle.MachineLearning.SVM
    let svm = SMO.C_SVC train_x train_y (Kernel.rbf 0.1f) 
                        { C_p = 1.0f; C_n = 1.0f; epsilon = 0.001f;
                          options = { strategy = SMO.SecondOrderInformation; maxIterations = 1000000; 
                                      shrinking = true; cacheSize = 200<MB> } }

### Prediction
Two class prediction function implements the decision rule $sign(\sum_{i=1}^l y_i\alpha_i K(\mathbf{x}_i, x) + b)$,
where $K$ - kernel function, $\alpha_i$ - $i$-th solution of the dual optimization problem,
$y_i$ - label of $i$-th support vector, $\mathbf{x}_i$ - $i$-th support vector, $b$ - bias value.

    TwoClass.predict svm x

## One Class Classification

### Training
Function `SMO.OneClass` finds a solution of the following optimization problem:
$$
\begin{array}
	\mathop{min}_{\mathbf{w},\rho,\mathbf{\xi}} & \quad \frac{1}{2} \mathbf{w}^T\mathbf{w} - \rho  + \frac{1}{\nu l} \sum_{i=1}^l \xi_i \\
    \text{subject to} & \quad \mathbf{w}^T\phi(\mathbf{x}_i) \geq \rho - \xi_i \\
	& \quad \xi_i \geq 0, i=1, \dots, l
\end{array}
$$
in the dual form:
$$
\begin{array}
	\mathop{min}_{\mathbf{\alpha}} & \quad \frac{1}{2} \mathbf{\alpha}^TQ\mathbf{w} \\
    \text{subject to} & \quad \mathbf{e}^T\mathbf{\alpha} = 1 \\
    & \quad 0 \leq \alpha_i \leq \frac{1}{\nu l}, i=1, \dots, l
\end{array}
$$
where $Q_{ij}=K(\mathbf{x}_i, \mathbf{x}_j)$.

    open Semagle.MachineLearning.SVM
    let svm = SMO.OneClass train_x (Kernel.rbf 0.1f) 
                           { nu = 0.5f; epsilon = 0.001f;
                             options = { strategy = SMO.SecondOrderInformation; maxIterations = 1000000; 
                                         shrinking = true; cacheSize = 200<MB> } }

### Prediction
Two class prediction function implements the decision rule $sign(\sum_{i=1}^l \alpha_i K(\mathbf{x}_i, x) + \rho)$,
where $K$ - kernel function, $\alpha_i$ - $i$-th solution of the dual optimization problem, 
$\mathbf{x}_i$ - $i$-th support vector, $\rho$ - bias value.

    OneClass.predict svm x


## Regression

### Training
Function `SMO.C_SVR` finds a solution of the following optimization problem:
$$
\begin{array}
	\mathop{min}_{\mathbf{w},b,\mathbf{\xi}} & \quad \frac{1}{2} \mathbf{w}^T\mathbf{w} + C \sum_{i=1}^l \xi_i + C \sum_{i=1}^l \xi_i^* \\
    \text{subject to} & \quad \mathbf{w}^T\phi(\mathbf{x}_i) + b - z_i \leq \eta + \xi_i \\
    & \quad z_i - \mathbf{w}^T\phi(\mathbf{x}_i) - b \leq \eta - \xi_i \\
	& \quad \xi_i, \xi_i^* \geq 0, i=1, \dots, l
\end{array}
$$
in the dual form:
$$
\begin{array}
	\mathop{min}_{\mathbf{\alpha}} & \quad \frac{1}{2} (\mathbf{\alpha} - \mathbf{\alpha}^*)^T Q (\mathbf{\alpha} - \mathbf{\alpha}^*) + 
        \eta \sum_{i=1}^l (\alpha_i + \alpha_i^*) + \sum_{i=1}^l (\alpha_i - \alpha_i^*) \\
    \text{subject to} & \quad \mathbf{e}^T(\mathbf{\alpha} - \mathbf{\alpha}^*) = 0 \\
    & \quad 0 \leq \alpha_i, \alpha_i^* \leq \frac{1}{\nu l}, i=1, \dots, l
\end{array}
$$
where $Q_{ij}=K(\mathbf{x}_i, \mathbf{x}_j)$.

    open Semagle.MachineLearning.SVM
    let svm = SMO.C_SVR train_x train_y (Kernel.rbf 0.1f) 
                        { eta = 0.1f; C = 1.0f; epsilon = 0.001f;
                          options = { strategy = SMO.SecondOrderInformation; maxIterations = 1000000; 
                                      shrinking = true; cacheSize = 200<MB> } }

### Prediction
Regression function computes the approximation function $\sum_{i=1}^l (-\alpha_i + \alpha_i^*) K(\mathbf{x}_i, x) + \rho)$,
$K$ - kernel function, $\alpha_i$ - $i$-th solution of the dual optimization problem,
$\mathbf{x}_i$ - $i$-th support vector, $\rho$ - bias value.

    Regression.predict svm x                                      