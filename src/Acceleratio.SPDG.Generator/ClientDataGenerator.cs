using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Acceleratio.SPDG.Generator.Objects;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.SharePoint.Client;
using Group = Microsoft.Azure.ActiveDirectory.GraphClient.Group;
using User = Microsoft.Azure.ActiveDirectory.GraphClient.User;

namespace Acceleratio.SPDG.Generator
{
    public partial class ClientDataGenerator : DataGenerator
    {
        List<string> _allUsers = null;
        List<string> _allGroups = null;
        protected override List<string> GetAvailableUsersInDirectory()
        {
            if (_allUsers == null)
            {
                var adClient = GetADClient();
                _allUsers = new List<string>();

                IPagedCollection<IUser> result = null;
                do
                {
                    result = adClient.Users.ExecuteAsync().Result;
                    foreach (var user in result.CurrentPage)
                    {
                        _allUsers.Add(user.UserPrincipalName);
                    }
                } while (result.MorePagesAvailable);
            }
            return _allUsers;
        }

        protected override List<string> GetAvailableGroupsInDirectory()
        {
            if (_allGroups == null)
            {
                var adClient = GetADClient();
                _allGroups = new List<string>();

                IPagedCollection<IGroup> result = null;
                do
                {
                    result = adClient.Groups.ExecuteAsync().Result;
                    foreach (var group in result.CurrentPage)
                    {
                        _allGroups.Add(group.DisplayName);
                    }
                } while (result.MorePagesAvailable);
            }
            return _allGroups;
        }

        protected new ClientGeneratorDefinition WorkingDefinition
        {
            get { return (ClientGeneratorDefinition)base.WorkingDefinition; }
        }

        public ClientDataGenerator(ClientGeneratorDefinition definition) : base(definition)
        {
        }

        protected override SPDGObjectsFactory CreateObjectsFactory()
        {
            return new SPDGClientObjectsFactory(new SharePointOnlineCredentials(WorkingDefinition.Username, Utilities.Common.StringToSecureString(WorkingDefinition.Password)));
        }

        private string GetToken()
        {
            return WorkingDefinition.AzureAdAccessToken;
        }
        private ActiveDirectoryClient GetADClient()
        {
            Uri servicePointUri = new Uri("https://graph.windows.net");
            
            var serviceRoot = new Uri(servicePointUri, string.Format("{0}.onmicrosoft.com", WorkingDefinition.TenantName));            
            
            ActiveDirectoryClient activeDirectoryClient = new ActiveDirectoryClient(serviceRoot,
                async () => GetToken());
            return activeDirectoryClient;
        }

        protected override void CreateUsersAndGroups()
        {
            var client = GetADClient();
            
            List<ITenantDetail> tenantsList = client.TenantDetails
                    //.Where(tenantDetail => tenantDetail.ObjectId.Equals())
                    .ExecuteAsync().Result.CurrentPage.ToList();
            ITenantDetail tenant = tenantsList.First();

            var defaultDomain = tenant.VerifiedDomains.First(x => x.@default.HasValue && x.@default.Value);
            if (WorkingDefinition.NumberOfUsersToCreate > 0)
            {
                try
                {
                    Log.Write("Creating Active Directory users.");

                    int batchcounter = 0;
                    HashSet<Tuple<string,string>> usedNames=new HashSet<Tuple<string, string>>();
                    for (int i = 0; i < WorkingDefinition.NumberOfUsersToCreate; i++)
                    {
                        try
                        {
                            var firstName = SampleData.GetSampleValueRandom(SampleData.FirstNames);
                            var lastName = SampleData.GetSampleValueRandom(SampleData.LastNames);
                            while (usedNames.Contains(new Tuple<string, string>(firstName, lastName)))
                            {
                                firstName = SampleData.GetSampleValueRandom(SampleData.FirstNames);
                                lastName = SampleData.GetSampleValueRandom(SampleData.LastNames);
                            }
                            usedNames.Add(new Tuple<string, string>(firstName, lastName));


                            var userPrincipal = new User();
                            userPrincipal.Surname = lastName;
                            userPrincipal.AccountEnabled = true;
                            
                            userPrincipal.GivenName = firstName;
                            userPrincipal.MailNickname = userPrincipal.GivenName.ToLower() + "." + userPrincipal.Surname.ToLower();
                            userPrincipal.UserPrincipalName = userPrincipal.MailNickname + "@"+ defaultDomain.Name;
                            
                            //userPrincipal.Name = userPrincipal.GivenName + " " + userPrincipal.Surname;                            
                            userPrincipal.DisplayName = userPrincipal.GivenName + " " + userPrincipal.Surname;
                            userPrincipal.UsageLocation = "US";                        
                            userPrincipal.PasswordProfile = new PasswordProfile
                            {
                                Password = "pass@word1",
                                ForceChangePasswordNextLogin = false,
                                
                            };
                            client.Users.AddUserAsync(userPrincipal,true).Wait();
                            batchcounter++;
                            if (batchcounter >= 50)
                            {
                                batchcounter = 0;
                                client.Context.SaveChangesAsync().Wait();
                            }

                        }
                        catch (Exception ex)
                        {
                            Errors.Log(ex);
                        }
                    }
                    if (batchcounter > 0)
                    {
                       client.Context.SaveChangesAsync().Wait();
                    }                   
                }
                catch (Exception ex)
                {
                    Errors.Log(ex);
                }
            }

            if (WorkingDefinition.NumberOfSecurityGroupsToCreate > 0)
            {
                try
                {
                    Log.Write("Creating Active Directory groups.");
                    HashSet<string> usedGroupNames=new HashSet<string>();
                    int batchCounter = 0;
                    for (int i = 0; i < WorkingDefinition.NumberOfSecurityGroupsToCreate; i++)
                    {
                        try
                        {
                            var displayName = SampleData.GetSampleValueRandom(SampleData.Accounts);
                            while (usedGroupNames.Contains(displayName))
                            {
                                displayName = SampleData.GetSampleValueRandom(SampleData.Accounts);
                            }

                            usedGroupNames.Add(displayName);
                            var mailNickname= Regex.Replace(displayName, @"[^a-z0-9]", "");
                            Group group = new Group();
                            group.DisplayName = displayName;
                            group.MailEnabled = false;
                            group.SecurityEnabled = true;
                            group.MailNickname = mailNickname;
                            batchCounter++;
                            
                            client.Groups.AddGroupAsync(group,true).Wait();
                            if (batchCounter >= 50)
                            {
                                batchCounter = 0;
                                client.Context.SaveChangesAsync().Wait();
                            }
                        }
                        catch (Exception ex)
                        {
                            Errors.Log(ex);
                        }
                        
                    }
                    if (batchCounter > 0)
                    {
                        client.Context.SaveChangesAsync().Wait();
                    }
                }
                catch (Exception ex)
                {
                    Errors.Log(ex);
                }

            }
        }

        protected override void ResolveWebAppsAndSiteCollections()
        {

            if (WorkingDefinition.CreateNewSiteCollections == 0)
            {
                base.workingSiteCollections.Add(new SiteCollInfo() {URL = WorkingDefinition.SiteCollection});
            }
            else
            {
                
                int totalProgress = WorkingDefinition.CreateNewSiteCollections;                  
                progressOverall("Creating Web Applications / Site Collections", totalProgress);
                
                ClientHelper helper=new ClientHelper(WorkingDefinition);
                HashSet<string> existingSites=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var siteCollectionUrl in helper.GetAllSiteCollections())
                {
                    existingSites.Add(siteCollectionUrl);
                }
                for (int s = 0; s < WorkingDefinition.CreateNewSiteCollections; s++)
                {
                    string siteName = "";
                    string siteUrl = "";
                    string leafName = "";
                    int i = 0;
                    string baseName = "";
                    do
                    {
                        siteName = SampleData.GetRandomName(SampleData.Companies, SampleData.Offices, null, ref i, out baseName);
                        leafName = Utilities.Path.GenerateSlug(siteName, 25);
                        siteUrl = string.Format("https://{0}.sharepoint.com/sites/{1}", WorkingDefinition.TenantName, leafName);
                    } while (existingSites.Contains(siteUrl));

                    progressDetail("Creating site collection '" + siteUrl + "'");
                    var owner = WorkingDefinition.SiteCollOwnerLogin;
                    if (string.IsNullOrEmpty(owner))
                    {
                        owner = WorkingDefinition.Username;
                    }
                    helper.CreateNewSiteCollection(siteName, leafName, owner);
                    SiteCollInfo siteCollInfo = new SiteCollInfo();
                    siteCollInfo.URL = siteUrl;
                    workingSiteCollections.Add(siteCollInfo);
                }               
            }            
            
        }
    }
}