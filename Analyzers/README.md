# Key notes

## How to subscribe

- __RegisterOperationAction__

      context.RegisterOperationAction(x => ActionX(x), operationKinds: OperationKind.SimpleAssignment);

called for each `operationKind` (like assignment in the above example) in the code

- __Start Block__

        context.RegisterOperationBlockStartAction(OnOperationBlockStart);
      }

      private static void OnOperationBlockStart(OperationBlockStartAnalysisContext context)

example of blocks:

    * Block Type        Symbol Kind     Example Code
    * Method body       IMethodSymbol   void M() { ... }
    * Constructor body	IMethodSymbol   public C() { ... }
    * Destructor body	IMethodSymbol   ~C() { ... }
    * Property accessor	IAccessorSymbol get { ... } / set { ... }
    * Event accessor	IAccessorSymbol add { ... } / remove { ... }
    * Local function	IMethodSymbol   void Local() { ... }

`OnOperationBlockStart` - called once for operation in the current block

- __End Block__

Called in the end of block processing after all steps inside block

      context.RegisterOperationBlockEndAction(c => AnalyzeDisposal(c, disposableVariables, disposeCalls, usingDeclarations));