﻿using System;
using System.ComponentModel;
using QuantBox.CSharp2CTP;
using SmartQuant.Data;
using SmartQuant.FIX;
using SmartQuant.Instruments;
using SmartQuant.Providers;

namespace QuantBox.OQ.CTP
{
    public partial class QBProvider:IMarketDataProvider
    {
        private IBarFactory factory;

        public event MarketDataRequestRejectEventHandler MarketDataRequestReject;
        public event MarketDataSnapshotEventHandler MarketDataSnapshot;
        public event BarEventHandler NewBar;
        public event BarEventHandler NewBarOpen;
        public event BarSliceEventHandler NewBarSlice;
        public event CorporateActionEventHandler NewCorporateAction;
        public event FundamentalEventHandler NewFundamental;
        public event BarEventHandler NewMarketBar;
        public event MarketDataEventHandler NewMarketData;
        public event MarketDepthEventHandler NewMarketDepth;
        public event QuoteEventHandler NewQuote;
        public event TradeEventHandler NewTrade;        

        #region IMarketDataProvider
        [Category(CATEGORY_BARFACTORY)]
        public IBarFactory BarFactory
        {
            get
            {
                return factory;
            }
            set
            {
                if (factory != null)
                {
                    factory.NewBar -= OnNewBar;
                    factory.NewBarOpen -= OnNewBarOpen;
                    factory.NewBarSlice -= OnNewBarSlice;
                }
                factory = value;
                if (factory != null)
                {
                    factory.NewBar += OnNewBar;
                    factory.NewBarOpen += OnNewBarOpen;
                    factory.NewBarSlice += OnNewBarSlice;
                }
            }
        }

        private void OnNewBar(object sender, BarEventArgs args)
        {
            if (NewBar != null)
            {
                NewBar(this, new BarEventArgs(args.Bar, args.Instrument, this));
            }
        }

        private void OnNewBarOpen(object sender, BarEventArgs args)
        {
            if (NewBarOpen != null)
            {
                NewBarOpen(this, new BarEventArgs(args.Bar, args.Instrument, this));
            }
        }

        private void OnNewBarSlice(object sender, BarSliceEventArgs args)
        {
            if (NewBarSlice != null)
            {
                NewBarSlice(this, new BarSliceEventArgs(args.BarSize, this));
            }
        }

        public void SendMarketDataRequest(FIXMarketDataRequest request)
        {
            
            lock (this)
            {
                switch (request.SubscriptionRequestType)
                {
                    case DataManager.MARKET_DATA_SUBSCRIBE:
                        if (!_bMdConnected)
                        {
                            EmitError(-1, -1, "行情服务器没有连接,无法订阅行情");
                            mdlog.ErrorFormat("行情服务器没有连接,无法订阅行情");
                            return;
                        }
                        for (int i = 0; i < request.NoRelatedSym; ++i)
                        {
                            FIXRelatedSymGroup group = request.GetRelatedSymGroup(i);

                            //通过订阅的方式，由平台传入合约对象，在行情接收处将要使用到合约
                            CThostFtdcDepthMarketDataField DepthMarket;
                            Instrument inst = InstrumentManager.Instruments[group.Symbol];
                            string altSymbol = inst.GetSymbol(Name);

                            if (!_dictDepthMarketData.TryGetValue(altSymbol, out DepthMarket))
                            {
                                DepthMarket = new CThostFtdcDepthMarketDataField();
                                _dictDepthMarketData.Add(altSymbol, DepthMarket);
                            }

                            _dictAltSymbol2Instrument[altSymbol] = inst;
                            mdlog.InfoFormat("订阅合约 {0}", altSymbol);
                            MdApi.MD_Subscribe(m_pMdApi, altSymbol);
                       
                        }
                        if (!_bTdConnected)
                        {
                            return;
                        }
                        TraderApi.TD_ReqQryInvestorPosition(m_pTdApi, null);
                        timerPonstion.Enabled = false;
                        timerPonstion.Enabled = true;
                        break;
                    case DataManager.MARKET_DATA_UNSUBSCRIBE:
                        if (!_bMdConnected)
                        {
                            mdlog.ErrorFormat("行情服务器没有连接，退订合约无效");
                            return;
                        }
                        for (int i = 0; i < request.NoRelatedSym; ++i)
                        {
                            FIXRelatedSymGroup group = request.GetRelatedSymGroup(i);

                            Instrument inst = InstrumentManager.Instruments[group.Symbol];
                            string altSymbol = inst.GetSymbol(Name);

                            _dictDepthMarketData.Remove(altSymbol);
                            mdlog.InfoFormat("取消订阅 {0}", altSymbol);
                            MdApi.MD_Unsubscribe(m_pMdApi, altSymbol);
                        }
                        break;
                    default:
                        throw new ArgumentException("Unknown subscription type: " + request.SubscriptionRequestType);
                }
            }
        }

        private void EmitNewQuoteEvent(IFIXInstrument instrument, Quote quote)
        {
            if (NewQuote != null)
            {
                NewQuote(this, new QuoteEventArgs(quote, instrument, this));
            }
            if (factory != null)
            {
                factory.OnNewQuote(instrument, quote);
            }
        }

        private void EmitNewTradeEvent(IFIXInstrument instrument, Trade trade)
        {
            if (NewTrade != null)
            {
                NewTrade(this, new TradeEventArgs(trade, instrument, this));
            }
            if (factory != null)
            {
                factory.OnNewTrade(instrument, trade);
            }
        }
        #endregion


        #region OpenQuant3接口的新方法
        public IMarketDataFilter MarketDataFilter { get; set; }
        #endregion
    }
}
