using System;

using PX.BarcodeProcessing;
using PX.Common;
using PX.Data;
using PX.Objects.PO;
using PX.Objects.PO.WMS;

using static PX.Objects.PO.WMS.ReceivePutAway;

using WMSBase = PX.Objects.IN.WMS.WarehouseManagementSystem<PX.Objects.PO.WMS.ReceivePutAway, PX.Objects.PO.WMS.ReceivePutAway.Host>;

namespace PX.Objects
{

	// Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
	public class ReceiveMode_RedirectExtension : ReceivePutAway.ScanExtension
	{
		[PXOverride]
		public virtual ScanMode<ReceivePutAway> DecorateScanMode(
			ScanMode<ReceivePutAway> original,
			Func<ScanMode<ReceivePutAway>, ScanMode<ReceivePutAway>> base_DecorateScanMode)
		{
			var mode = base_DecorateScanMode(original);

			if (mode is ReceivePutAway.ReceiveMode receiveMode)
			{
				receiveMode
					/// We can replace or append logic to Validate,Apply,ReportSuccess, etc.
					.Intercept.CreateRedirects.ByAppend(() => new[] {
						new RedirectFrom<ReceivePutAway>() });
			}

			return mode;
		}

		public sealed class RedirectFrom<TForeignBasis> : WMSBase.RedirectFrom<TForeignBasis>.SetMode<PutAwayMode>
			where TForeignBasis : PXGraphExtension, IBarcodeDrivenStateMachine
		{
			public override string Code => "DPA";
			public override string DisplayName => Msg.DisplayName;

			private string RefNbr { get; set; }

			public override bool IsPossible
			{
				get
				{
					bool wmsReceiving = PXAccess.FeatureInstalled<CS.FeaturesSet.wMSReceiving>();
					var rpSetup = POReceivePutAwaySetup.PK.Find(Basis.Graph, Basis.Graph.Accessinfo.BranchID);
					return wmsReceiving && rpSetup?.ShowReceivingTab != false;
				}
			}

			protected override bool PrepareRedirect()
			{
				if (Basis is ReceivePutAway)
				{
					var rpa = Basis as ReceivePutAway;
					if (rpa.RefNbr != null)
					{
						if (rpa.FindMode<ReceiveMode>().TryValidate(rpa.Receipt).By<ReceiptState>() is Validation valid && valid.IsError == true)
						{
							rpa.ReportError(valid.Message, valid.MessageArgs);
							return false;
						}
						else
							RefNbr = rpa.RefNbr;
					}
				}

				return true;
			}

			protected override void CompleteRedirect()
			{
				if (Basis is ReceivePutAway)
				{
					var rpa = Basis as ReceivePutAway;
					if (rpa.CurrentMode.Code != ReturnMode.Value && this.RefNbr != null)
						if (rpa.TryProcessBy(ReceivePutAway.ReceiptState.Value, RefNbr, StateSubstitutionRule.KeepAll & ~StateSubstitutionRule.KeepPositiveReports))
						{
							rpa.SetDefaultState();
							RefNbr = null;
						}
				}
			}

			#region Messages
			[PXLocalizable]
			public abstract class Msg
			{
				public const string DisplayName = "Directed Put Away";
			}
			#endregion
		}
	}

}

