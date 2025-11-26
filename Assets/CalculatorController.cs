using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
// using TMPro; // Uncomment if you use TextMeshPro

public class CalculatorController : MonoBehaviour
{
    public TMP_Text expressionText;
    public TMP_Text resultText;

    private StringBuilder expression = new StringBuilder();
    private bool lastInputIsOperator = false;
    private bool hasDecimalInCurrentNumber = false;

    void Start()
    {
        UpdateDisplay();
    }

    // Called by digit buttons (0..9)
    public void OnDigit(string digit)
    {
        if (digit == null) return;
        expression.Append(digit);
        lastInputIsOperator = false;
        UpdateDisplay();
    }

    // Called by decimal button
    public void OnDecimal()
    {
        // If last input was operator or expression empty, we want "0."
        if (expression.Length == 0 || lastInputIsOperator)
        {
            expression.Append("0.");
            hasDecimalInCurrentNumber = true;
            lastInputIsOperator = false;
            UpdateDisplay();
            return;
        }

        // prevent multiple decimals in same number
        // We'll scan backwards until operator or start
        for (int i = expression.Length - 1; i >= 0; i--)
        {
            char c = expression[i];
            if (c == '.') return; // already decimal in current number
            if (IsOperator(c)) break;
        }

        expression.Append('.');
        hasDecimalInCurrentNumber = true;
        lastInputIsOperator = false;
        UpdateDisplay();
    }

    // Called by operator buttons: "+", "-", "*", "/"
    public void OnOperator(string op)
    {
        if (string.IsNullOrEmpty(op) || op.Length != 1) return;
        char opChar = op[0];

        if (expression.Length == 0)
        {
            // allow unary minus to start a negative number
            if (opChar == '-')
            {
                expression.Append("0-");
                lastInputIsOperator = true;
                UpdateDisplay();
            }
            // ignore other operators at start
            return;
        }

        // if last input was operator, replace it (so user can correct operator)
        if (lastInputIsOperator)
        {
            // Replace last operator
            expression[expression.Length - 1] = opChar;
        }
        else
        {
            expression.Append(opChar);
            hasDecimalInCurrentNumber = false;
            lastInputIsOperator = true;
        }

        UpdateDisplay();
    }

    // Clear current entry - like clearing last number (AC often used as all clear; we have both)
    public void OnClearEntry()
    {
        // Implementation: remove last token (number or operator)
        if (expression.Length == 0) return;

        int i = expression.Length - 1;
        // if last char is operator, remove it
        if (IsOperator(expression[i]))
        {
            expression.Remove(i, 1);
            lastInputIsOperator = expression.Length > 0 && IsOperator(expression[expression.Length - 1]);
            UpdateDisplay();
            return;
        }

        // else remove the last number (digits & dot)
        while (i >= 0 && !IsOperator(expression[i]))
        {
            i--;
        }

        expression.Remove(i + 1, expression.Length - (i + 1));
        // update flags
        lastInputIsOperator = (expression.Length > 0 && IsOperator(expression[expression.Length - 1]));
        UpdateDisplay();
    }

    // Reset all (clear expression and result)
    public void OnResetAll()
    {
        expression.Clear();
        resultText.text = "0";
        lastInputIsOperator = false;
        hasDecimalInCurrentNumber = false;
        UpdateDisplay();
    }

    // Equals button
    public void OnEquals()
    {
        string expr = expression.ToString();
        if (string.IsNullOrWhiteSpace(expr)) return;

        try
        {
            List<string> tokens = Tokenize(expr);
            List<string> rpn = ConvertToRPN(tokens);
            double result = EvaluateRPN(rpn);
            // Format result: if integer, show without decimal point
            string outStr;
            if (Math.Abs(result - Math.Round(result)) < 1e-12)
                outStr = Math.Round(result).ToString(CultureInfo.InvariantCulture);
            else
                outStr = result.ToString("G12", CultureInfo.InvariantCulture); // up to 12 significant digits

            resultText.text = outStr;
        }
        catch (Exception ex)
        {
            resultText.text = "Error";
            Debug.LogWarning("Evaluation error: " + ex.Message);
        }
    }

    private void UpdateDisplay()
    {
        expressionText.text = expression.Length > 0 ? expression.ToString() : "0";
        // Optionally update resultText with a running evaluation preview (comment out if not desired)
        // TryRunPreview();
    }

    // ---- Tokenizer ----
    private List<string> Tokenize(string expr)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (IsOperator(c))
            {
                // handle unary minus (if at start or previous token is operator)
                if (c == '-' && (tokens.Count == 0 || IsOperator(tokens[tokens.Count - 1][0])))
                {
                    // unary minus: treat it as "0" and "-" operator; easier is to attach minus to number parsing
                    // We'll build a negative number token
                    int j = i + 1;
                    var numSb = new StringBuilder();
                    numSb.Append('-');

                    // allow digits and decimal after unary minus
                    while (j < expr.Length && (char.IsDigit(expr[j]) || expr[j] == '.'))
                    {
                        numSb.Append(expr[j]);
                        j++;
                    }

                    // if there is at least one digit after '-', accept it
                    if (numSb.Length > 1)
                    {
                        tokens.Add(numSb.ToString());
                        i = j;
                        continue;
                    }
                    else
                    {
                        // just a minus operator, treat normally
                        tokens.Add("-"); i++; continue;
                    }
                }
                else
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }
            }

            // number parsing: digits and decimal
            if (char.IsDigit(c) || c == '.')
            {
                int j = i;
                var numSb = new StringBuilder();
                bool dotSeen = false;
                while (j < expr.Length && (char.IsDigit(expr[j]) || expr[j] == '.'))
                {
                    if (expr[j] == '.')
                    {
                        if (dotSeen) throw new Exception("Invalid number format");
                        dotSeen = true;
                    }
                    numSb.Append(expr[j]);
                    j++;
                }

                tokens.Add(numSb.ToString());
                i = j;
                continue;
            }

            // unknown char
            throw new Exception($"Unknown character in expression: {c}");
        }

        return tokens;
    }

    // ---- Shunting-Yard: convert tokens to RPN ----
    private List<string> ConvertToRPN(List<string> tokens)
    {
        var output = new List<string>();
        var opStack = new Stack<string>();

        foreach (var token in tokens)
        {
            double dummy;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out dummy))
            {
                output.Add(token);
            }
            else if (IsOperator(token[0]))
            {
                while (opStack.Count > 0 && IsOperator(opStack.Peek()[0]) &&
                       ((GetPrecedence(opStack.Peek()) > GetPrecedence(token)) ||
                        (GetPrecedence(opStack.Peek()) == GetPrecedence(token) && IsLeftAssociative(token))))
                {
                    output.Add(opStack.Pop());
                }
                opStack.Push(token);
            }
            else
            {
                throw new Exception("Unsupported token: " + token);
            }
        }

        while (opStack.Count > 0)
        {
            var op = opStack.Pop();
            output.Add(op);
        }

        return output;
    }

    // ---- Evaluate RPN ----
    private double EvaluateRPN(List<string> rpn)
    {
        var stack = new Stack<double>();
        foreach (var token in rpn)
        {
            double val;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
            {
                stack.Push(val);
            }
            else if (IsOperator(token[0]))
            {
                if (stack.Count < 2) throw new Exception("Malformed expression");
                double b = stack.Pop();
                double a = stack.Pop();
                double res = ApplyOperator(a, b, token[0]);
                stack.Push(res);
            }
            else
            {
                throw new Exception("Unknown token in RPN: " + token);
            }
        }

        if (stack.Count != 1) throw new Exception("Malformed expression after evaluation");

        return stack.Pop();
    }

    // ---- Helpers ----
    private static bool IsOperator(char c)
    {
        return c == '+' || c == '-' || c == '*' || c == '/';
    }

    private static int GetPrecedence(string op)
    {
        if (op == "*" || op == "/") return 2;
        if (op == "+" || op == "-") return 1;
        return 0;
    }

    private static bool IsLeftAssociative(string op)
    {
        // all are left-associative here
        return true;
    }

    private static double ApplyOperator(double a, double b, char op)
    {
        switch (op)
        {
            case '+': return a + b;
            case '-': return a - b;
            case '*': return a * b;
            case '/':
                if (Math.Abs(b) < 1e-15) throw new DivideByZeroException();
                return a / b;
            default: throw new Exception("Unsupported operator: " + op);
        }
    }
}
