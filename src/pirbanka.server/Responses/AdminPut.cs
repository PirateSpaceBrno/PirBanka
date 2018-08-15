﻿using JamesWright.SimpleHttp;
using PirBanka.Server.Controllers;
using PirBanka.Server.Models.Db;
using PirBanka.Server.Models.Put;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PirBanka.Server.Responses
{
    public class AdminPut
    {
        public static KeyValuePair<Regex, Action<Request, Response>> Action(string endpoint)
        {
            var action = Actions.FirstOrDefault(x => x.Key == endpoint);

            if (!action.Equals(new KeyValuePair<string, Action<Request, Response>>()))
            {
                return new KeyValuePair<Regex, Action<Request, Response>>(new Regex(action.Key), action.Value);
            }

            return new KeyValuePair<Regex, Action<Request, Response>>(new Regex(endpoint), JamesWright.SimpleHttp.Actions.Error404);
        }

        public static Dictionary<string, Action<Request, Response>> Actions = new Dictionary<string, Action<Request, Response>>()
        {
            {
                @"^/currencies/(\d+)$",
                new Action<Request, Response>( async (req, res) =>
                {
                    int currencyId = TextHelper.GetUriIds(req.Endpoint, @"^/currencies/(\d+)$")[1];

                    // Authorized request
                    var auth = HttpAuth.AuthenticateHttpRequest(req.UserIdentity, HttpAuth.AccessLevel.Administrator);
                    if (auth != null)
                    {
                        var request = await req.GetBodyAsync();

                        var currencyUpdate = JsonHelper.DeserializeObject<CurrenciesUpdate>(request);
                        if (currencyUpdate != null && currencyId != PirBankaHelper.GeneralCurrency.id)
                        {
                            try
                            {
                                Server.db.BeginTransaction();

                                var currency = Server.db.Get<Currency>(DatabaseHelper.Tables.currencies, $"id={currencyId}");

                                if(currency != null)
                                {
                                    if(currencyUpdate.name != "" && currency.name != currencyUpdate.name)
                                    {
                                        currency.name = currencyUpdate.name;
                                        Server.db.Update(DatabaseHelper.Tables.currencies, currency);
                                    }

                                    if(currencyUpdate.shortname != "" && currency.shortname != currencyUpdate.shortname)
                                    {
                                        currency.shortname = currencyUpdate.shortname;
                                        Server.db.Update(DatabaseHelper.Tables.currencies, currency);
                                    }

                                    if(currencyUpdate.exchangeRate != null)
                                    {
                                        if(Math.Abs((decimal)currencyUpdate.exchangeRate).Equals(currencyUpdate.exchangeRate))
                                        {
                                            var currencyView = Server.db.Get<CurrencyView>(DatabaseHelper.Tables.currencies_view, $"id={currencyId}");
                                            if(currencyView.rate != currencyUpdate.exchangeRate)
                                            {
                                                ExchangeRates newExchangeRate = new ExchangeRates()
                                                {
                                                    currency = currency.id,
                                                    rate = (decimal)currencyUpdate.exchangeRate
                                                };

                                                Server.db.Insert(DatabaseHelper.Tables.exchange_rates, newExchangeRate);
                                            }
                                        }
                                        else
                                        {
                                            throw new Exception("Currency exchange rate cannot be negative");
                                        }
                                    }

                                    Server.db.CompleteTransaction();
                                    res.Content = $"Currency {currency.name} updated.";
                                    res.StatusCode = StatusCodes.Success.Created;
                                }
                                else
                                {
                                    throw new Exception("Trying to update nonexisting currency.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Server.db.AbortTransaction();

                                res.Content = $"Currency update failed: {ex.Message}";
                                res.StatusCode = StatusCodes.Redirection.NotModified;
                                Console.WriteLine($"WARN - {ex.Message}");
                            }
                        }
                        else
                        {
                            res.Content = $"Invalid data for Currency update.";
                            res.StatusCode = StatusCodes.ClientError.BadRequest;
                        }
                    }
                    else
                    {
                        res.Content = "Unauthorized";
                        res.StatusCode = StatusCodes.ClientError.Unauthorized;
                    }

                    res.ContentType = ContentTypes.Html;
                    await res.SendAsync();
                }
            )},
            //{
            //    @"",
            //    new Action<Request, Response>( async (req, res) =>
            //    {

            //    }
            //)},
        };
    }
}