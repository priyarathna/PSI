// ⓅⓈⒾ  ●  Pascal Language System  ●  Academy'23
// TypeAnalyze.cs ~ Type checking, type coercion
// ─────────────────────────────────────────────────────────────────────────────
namespace PSI;
using static NType;
using static Token.E;

public class TypeAnalyze : Visitor<NType> {
   public TypeAnalyze () {
      mSymbols = SymTable.Root;
   }
   SymTable mSymbols;

   #region Declarations ------------------------------------
   public override NType Visit (NProgram p) 
      => Visit (p.Block);
   
   public override NType Visit (NBlock b) {
      mSymbols = new SymTable { Parent = mSymbols };
      Visit (b.Declarations); Visit (b.Body);
      mSymbols = mSymbols.Parent;
      return Void;
   }

   public override NType Visit (NDeclarations d) {
      Visit (d.Consts); Visit (d.Vars); return Visit (d.Funcs);
   }

   public override NType Visit (NVarDecl d) {
      if (mSymbols.Find (d.Name.Text, true) is NVarDecl variable && variable.Name.Source != null)
         throw new ParseException (d.Name, "Variable already defined in this scope");
      switch (mSymbols.Find (d.Name.Text)) {
         case NFnDecl:
            throw new ParseException (d.Name, "Variable name is same as function name");
         case NConstDecl:
            throw new ParseException (d.Name, "Variable name is same as const name");
         default: if(!mSymbols.Vars.Contains(d)) mSymbols.Vars.Add (d); break;
      }
      return d.Type;
   }

   public override NType Visit (NFnDecl f) {
      if (mSymbols.Find (f.Name.Text) is NConstDecl)
         throw new ParseException (f.Name, "Function name is same as const name");
      mSymbols.Funcs.Add (f);
      return f.Return;
   }

   public override NType Visit (NConstDecl c) {
      if (mSymbols.Find (c.Name.Text) is NFnDecl)
         throw new ParseException (c.Name, "Const name is same as function name");
      mSymbols.Consts.Add (c);
      return c.Type = GetType (c.Value.Kind);
   }
   #endregion

   #region Statements --------------------------------------
   public override NType Visit (NCompoundStmt b)
      => Visit (b.Stmts);

   public override NType Visit (NAssignStmt a) {
      NType type;
      NVarDecl? varDecl = null;
      switch (mSymbols.Find (a.Name.Text)) {
         case NFnDecl f: type = f.Return; break;
         case NVarDecl v: type = v.Type; varDecl = v; break;
         default: throw new ParseException (a.Name, "Unknown variable");
      }
      a.Expr.Accept (this);
      a.Expr = AddTypeCast (a.Name, a.Expr, type);
      if (varDecl != null && !mDefinedChars.Contains (a.Name.Text)) mDefinedChars.Add (a.Name.Text);
      return type;
   }
   
   NExpr AddTypeCast (Token token, NExpr source, NType target) {
      if (source.Type == target) return source;
      bool valid = (source.Type, target) switch {
         (Int, Real) or (Char, Int) or (Char, String) => true,
         _ => false
      };
      if (!valid) throw new ParseException (token, "Invalid type");
      return new NTypeCast (source) { Type = target };
   }

   public override NType Visit (NWriteStmt w)
      => Visit (w.Exprs);

   public override NType Visit (NIfStmt f) {
      f.Condition.Accept (this);
      f.IfPart.Accept (this); f.ElsePart?.Accept (this);
      return Void;
   }

   public override NType Visit (NForStmt f) {
      var name = f.Var.Text;
      if (mSymbols.Find (name) is not NVarDecl)
         throw new ParseException (f.Var, "Unknown variable");
      if (!mDefinedChars.Contains (name)) mDefinedChars.Add (name);
      f.Start.Accept (this); f.End.Accept (this); f.Body.Accept (this);
      return Void;
   }

   public override NType Visit (NReadStmt r) {
      throw new NotImplementedException ();
   }

   public override NType Visit (NWhileStmt w) {
      w.Condition.Accept (this); w.Body.Accept (this);
      return Void; 
   }

   public override NType Visit (NRepeatStmt r) {
      Visit (r.Stmts); r.Condition.Accept (this);
      return Void;
   }

   public override NType Visit (NCallStmt c) {
      throw new NotImplementedException ();
   }
   #endregion

   #region Expression --------------------------------------
   public override NType Visit (NLiteral t) => t.Type = GetType (t.Value.Kind);

   public override NType Visit (NUnary u) 
      => u.Expr.Accept (this);

   public override NType Visit (NBinary bin) {
      NType a = bin.Left.Accept (this), b = bin.Right.Accept (this);
      bin.Type = (bin.Op.Kind, a, b) switch {
         (ADD or SUB or MUL or DIV, Int or Real, Int or Real) when a == b => a,
         (ADD or SUB or MUL or DIV, Int or Real, Int or Real) => Real,
         (MOD, Int, Int) => Int,
         (ADD, String, _) => String, 
         (ADD, _, String) => String,
         (LT or LEQ or GT or GEQ, Int or Real, Int or Real) => Bool,
         (LT or LEQ or GT or GEQ, Int or Real or String or Char, Int or Real or String or Char) when a == b => Bool,
         (EQ or NEQ, _, _) when a == b => Bool,
         (EQ or NEQ, Int or Real, Int or Real) => Bool,
         (AND or OR, Int or Bool, Int or Bool) when a == b => a,
         _ => Error,
      };
      if (bin.Type == Error)
         throw new ParseException (bin.Op, "Invalid operands");
      var (acast, bcast) = (bin.Op.Kind, a, b) switch {
         (_, Int, Real) => (Real, Void),
         (_, Real, Int) => (Void, Real), 
         (_, String, not String) => (Void, String),
         (_, not String, String) => (String, Void),
         _ => (Void, Void)
      };
      if (acast != Void) bin.Left = new NTypeCast (bin.Left) { Type = acast };
      if (bcast != Void) bin.Right = new NTypeCast (bin.Right) { Type = bcast };
      return bin.Type;
   }

   public override NType Visit (NIdentifier d) {
      if (mSymbols.Find (d.Name.Text) is NVarDecl v) {
         if (!mDefinedChars.Contains (d.Name.Text)) throw new ParseException (d.Name, $"Use of unassigned local variable");
         return d.Type = v.Type;
      }
      if (mSymbols.Find (d.Name.Text) is NConstDecl c)
         return d.Type = c.Type;
      throw new ParseException (d.Name, "Unknown variable");
   }

   public override NType Visit (NFnCall f) {
      var fName = f.Name;
      if (mSymbols.Find (fName.Text) is NFnDecl x) {
         var len = f.Params.Length;
         if (x.Params.Length != len) throw new ParseException (fName, $"No overload for this function takes {len} arguments");
         for (int i = 0; i < len; i++) {
            NType type1 = f.Params[i].Accept (this), type2 = x.Params[i].Accept (this);
            if (type1 != type2) f.Params[i] = AddTypeCast (fName, f.Params[i], type2);
         }
         x.Body?.Accept (this);
         return f.Type = x.Return;
      }
      throw new ParseException (fName, "Unknown function");
   }

   public override NType Visit (NTypeCast c) {
      c.Expr.Accept (this); return c.Type;
   }
   #endregion

   NType Visit (IEnumerable<Node> nodes) {
      foreach (var node in nodes) node.Accept (this);
      return NType.Void;
   }

   NType GetType (Token.E kind) => kind switch {
      L_INTEGER => Int, L_REAL => Real, L_BOOLEAN => Bool, L_STRING => String,
      L_CHAR => Char, _ => Error,
   };

   List<string> mDefinedChars = new ();
}
