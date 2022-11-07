using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PX.Objects.IN.WMS;
using PX.Objects.AM;
using PX.Common;
using PX.Data;
using PX.Objects.IN;
using PX.BarcodeProcessing;

namespace scanmoveqty
{
    public class ScanQtyExt : ScanMove.ScanExtension // an extension component of the barcode driven state machine
    {
        public static bool IsActive() => true;


        public class QtyState : ScanMove.EntityState<decimal?>
        {
            public BarcodeQtySupport<ScanMove, ScanMove.Host> QtyBase => BarcodeQtySupport<ScanMove, ScanMove.Host>.GetSelf(Basis);
            protected override void Apply(decimal? value) => QtyBase.QtySetter.WithEventFiring.Set(x => x.Qty, value);

            public override string Code => "UsrQTY";

            protected override string StatePrompt => "Scan Quantity";

            protected override decimal? GetByBarcode(string barcode)
            {
                if (barcode.StartsWith("%"))
                    barcode = barcode.Substring(1);

                return decimal.TryParse(barcode, out decimal value)
                    ? Math.Abs(value)
                    : (decimal?)null;
            }
        }

        [PXOverride]
        public virtual ScanMode<ScanMove> DecorateScanMode(
        ScanMode<ScanMove> original, // however while decorating components only the ScanComponent<WMS> form must be used, you can not override this method using the WMS.ScanComponent form
        Func<ScanMode<ScanMove>, ScanMode<ScanMove>> base_DecorateScanMode)
        {
            var mode = base_DecorateScanMode(original);

            if (mode is ScanMove.MoveMode moveMode)
            {
                //moveMode
                moveMode.Intercept.CreateStates.ByAppend(basis => new[] { new QtyState() });

                moveMode
                  // the ByAppend overriding strategy helps to simplify adding new components without even touching the existing ones
                  .Intercept.CreateTransitions.ByReplace(() =>
                  {
                      return Base1.StateFlow(flow => flow
                         .From<ScanMove.OrderTypeState>()
                         .NextTo<ScanMove.ProdOrdState>()
                         .NextTo<ScanMove.OperationState>()
                         .NextTo<QtyState>()
                         .NextTo<ScanMove.WarehouseState>()
                         .NextTo<ScanMove.LocationState>()
                         .NextTo<ScanMove.LotSerialState>()
                         .NextTo<ScanMove.ExpireDateState>()
                         .NextTo<ScanMove.ConfirmationState>()
                    );
                  });
            }
            return mode;
        }
    }
}
