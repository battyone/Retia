﻿using QuickGraph;

namespace Retia.Analytical
{
    public class ExprGraph : BidirectionalGraph<Expr, Edge<Expr>>
    {
        public ExprGraph() : base(false)
        {
        }
    }
}