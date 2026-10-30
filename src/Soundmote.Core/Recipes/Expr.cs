using System.Globalization;

namespace Soundmote.Core.Recipes;

/// <summary>
/// A small arithmetic expression from a recipe, such as <c>250 + 2300 * wave * amount^0.8</c> (S10). Parsed once
/// against a list of names; evaluating reads numbered slots and never allocates, so it can run per block.
/// </summary>
public abstract class Expr
{
    public const int MaxLength = 240;

    public abstract float Eval(float[] slots);

    public static Expr Constant(float value) => new Const(value);

    /// <summary>Parses <paramref name="text"/>; every name must be in <paramref name="names"/>, whose index is its slot.</summary>
    /// <exception cref="RecipeException">The text is not a valid expression or uses an unknown name.</exception>
    public static Expr Parse(string text, IReadOnlyList<string> names)
    {
        if (text.Length > MaxLength) throw new RecipeException($"Expression is longer than {MaxLength} characters: {text[..40]}…");
        var parser = new Parser(text, names);
        var expr = parser.Sum();
        parser.End();
        return expr;
    }

    private sealed class Const(float value) : Expr
    {
        public override float Eval(float[] slots) => value;
    }

    private sealed class Slot(int index) : Expr
    {
        public override float Eval(float[] slots) => slots[index];
    }

    private sealed class Negate(Expr a) : Expr
    {
        public override float Eval(float[] slots) => -a.Eval(slots);
    }

    private sealed class Binary(char op, Expr a, Expr b) : Expr
    {
        public override float Eval(float[] slots)
        {
            var x = a.Eval(slots);
            var y = b.Eval(slots);
            return op switch
            {
                '+' => x + y,
                '-' => x - y,
                '*' => x * y,
                '/' => MathF.Abs(y) < 1e-9f ? 0f : x / y,
                _ => Power(x, y)
            };
        }
    }

    private sealed class Call(string name, Expr[] args) : Expr
    {
        public override float Eval(float[] slots)
        {
            var x = args[0].Eval(slots);
            return name switch
            {
                "sqrt" => x <= 0 ? 0 : MathF.Sqrt(x),
                "abs" => MathF.Abs(x),
                "min" => MathF.Min(x, args[1].Eval(slots)),
                "max" => MathF.Max(x, args[1].Eval(slots)),
                "pow" => Power(x, args[1].Eval(slots)),
                "clamp" => Math.Clamp(x, args[1].Eval(slots), MathF.Max(args[1].Eval(slots), args[2].Eval(slots))),
                "step" => x >= args[1].Eval(slots) ? 1f : 0f,
                _ => 0f
            };
        }
    }

    /// <summary>Powers of negative numbers are 0, so a recipe can never produce NaN this way.</summary>
    private static float Power(float x, float y) => x <= 0 ? (x == 0 && y <= 0 ? 1 : 0) : MathF.Pow(x, y);

    private static readonly Dictionary<string, int> Arity = new()
    {
        ["sqrt"] = 1, ["abs"] = 1, ["min"] = 2, ["max"] = 2, ["pow"] = 2, ["clamp"] = 3, ["step"] = 2
    };

    /// <summary>Recursive descent: sums of products of signed powers of atoms.</summary>
    private sealed class Parser(string text, IReadOnlyList<string> names)
    {
        private int _pos;

        public Expr Sum()
        {
            var left = Product();
            while (Peek() is '+' or '-')
            {
                var op = text[_pos++];
                left = new Binary(op, left, Product());
            }
            return left;
        }

        public void End()
        {
            if (Peek() != '\0') throw Error($"unexpected '{text[_pos]}'");
        }

        private Expr Product()
        {
            var left = Unary();
            while (Peek() is '*' or '/')
            {
                var op = text[_pos++];
                left = new Binary(op, left, Unary());
            }
            return left;
        }

        /// <summary>A sign applies to the whole power, as in arithmetic: -2^2 is -4.</summary>
        private Expr Unary()
        {
            if (Peek() == '-')
            {
                _pos++;
                return new Negate(Unary());
            }
            if (Peek() == '+')
            {
                _pos++;
                return Unary();
            }
            return Power();
        }

        /// <summary>Right-associative: 2^3^2 is 2^9. The exponent may carry its own sign.</summary>
        private Expr Power()
        {
            var left = Atom();
            if (Peek() != '^') return left;
            _pos++;
            return new Binary('^', left, Unary());
        }

        private Expr Atom()
        {
            var c = Peek();
            if (c == '(')
            {
                _pos++;
                var inner = Sum();
                Expect(')');
                return inner;
            }
            if (char.IsDigit(c) || c == '.') return Number();
            if (char.IsLetter(c)) return Name();
            throw Error(c == '\0' ? "the expression ends too early" : $"unexpected '{c}'");
        }

        private Expr Number()
        {
            var start = _pos;
            while (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] == '.')) _pos++;
            if (_pos < text.Length && text[_pos] is 'e' or 'E')
            {
                _pos++;
                if (_pos < text.Length && text[_pos] is '+' or '-') _pos++;
                while (_pos < text.Length && char.IsDigit(text[_pos])) _pos++;
            }
            var token = text[start.._pos];
            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Error($"'{token}' is not a number");
            return new Const(value);
        }

        private Expr Name()
        {
            var start = _pos;
            while (_pos < text.Length && (char.IsLetterOrDigit(text[_pos]) || text[_pos] == '_')) _pos++;
            var name = text[start.._pos];
            if (Peek() == '(')
            {
                if (!Arity.TryGetValue(name, out var count)) throw Error($"unknown function '{name}'");
                _pos++;
                var args = new List<Expr> { Sum() };
                while (Peek() == ',')
                {
                    _pos++;
                    args.Add(Sum());
                }
                Expect(')');
                if (args.Count != count) throw Error($"{name} takes {count} argument{(count == 1 ? "" : "s")}");
                return new Call(name, args.ToArray());
            }
            for (var i = 0; i < names.Count; i++)
                if (names[i] == name) return new Slot(i);
            throw Error($"unknown name '{name}'");
        }

        private char Peek()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos])) _pos++;
            return _pos < text.Length ? text[_pos] : '\0';
        }

        private void Expect(char c)
        {
            if (Peek() != c) throw Error($"expected '{c}'");
            _pos++;
        }

        private RecipeException Error(string what) => new($"In \"{text}\": {what}.");
    }
}

/// <summary>A recipe that cannot be played, with a message that says where and why.</summary>
public sealed class RecipeException(string message) : Exception(message);
