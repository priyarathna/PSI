namespace PSI;

public class ExprXML : Visitor<XElement> {
   public override XElement Visit (NLiteral literal) => 
      new XElement ("Literal", new XAttribute ("Value", literal.Value.Text), new XAttribute ("Type", literal.Type));

   public override XElement Visit (NIdentifier identifier) =>
      new XElement ("Ident", new XAttribute ("Name", identifier.Name), new XAttribute ("Type", identifier.Type));

   public override XElement Visit (NUnary unary) =>
      new XElement ("Unary", new XAttribute ("Op", unary.Op.Kind), new XAttribute ("Type", unary.Type), unary.Expr.Accept (this));

   public override XElement Visit (NBinary binary) =>
      new XElement ("Binary", new XAttribute ("Op", binary.Op.Kind), new XAttribute ("Type", binary.Type), binary.Left.Accept (this), binary.Right.Accept (this));
}