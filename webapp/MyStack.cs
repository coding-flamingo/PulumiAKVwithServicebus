using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;
using Pulumi.Azure.AppService;
using Pulumi.Azure.KeyVault;
using Pulumi.Azure.KeyVault.Inputs;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.AppInsights;
using AzureAD = Pulumi.AzureAD;

class MyStack : Stack
{
    public MyStack()
    {
       // Create an Azure Resource Group
        var resourceGroup = new ResourceGroup("pulumiTestRG");

        //create service bus
        var serviceBus = new Pulumi.Azure.ServiceBus.Namespace("webappServiceBus", new Pulumi.Azure.ServiceBus.NamespaceArgs
        {
            Location = resourceGroup.Location,
            ResourceGroupName = resourceGroup.Name,
            Sku = "Standard",
        });

        //create ServiceBus Queue
        var exampleQueue = new Pulumi.Azure.ServiceBus.Queue("exampleQueue", new Pulumi.Azure.ServiceBus.QueueArgs
        {
            ResourceGroupName = resourceGroup.Name,
            NamespaceName = serviceBus.Name,
        });

        var appServicePlan = new Plan("asp", new PlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "App",
            Sku = new PlanSkuArgs
            {
                Tier = "Basic",
                Size = "B1",
            },
        });

        // var appInsights = new Insights("appInsights", new InsightsArgs
        // {
        //     ApplicationType = "web",
        //     ResourceGroupName = resourceGroup.Name
        // });

        var webapp = new AppService("webapp", new AppServiceArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AppServicePlanId = appServicePlan.Id,
            AppSettings =
            {
                // {"WEBSITE_RUN_FROM_PACKAGE", codeBlobUrl},
                // {"APPINSIGHTS_INSTRUMENTATIONKEY", appInsights.InstrumentationKey},
                // {"APPLICATIONINSIGHTS_CONNECTION_STRING", appInsights.InstrumentationKey.Apply(key => $"InstrumentationKey={key}")},
                {"ApplicationInsightsAgent_EXTENSION_VERSION", "~2"},
            },
            HttpsOnly = true,
            Identity = new AppServiceIdentityArgs
            {
                Type = "SystemAssigned"
            },
        });
        //get CurentUser
        var current = Output.Create(Pulumi.Azure.Core.GetClientConfig.InvokeAsync());
        
        //create aad group
        var appADGroup = new AzureAD.Group("mynewGroup", new AzureAD.GroupArgs
        {
            Owners = { current.Apply(current => current.ObjectId) },
            Members = { webapp.Identity.Apply(x => x.PrincipalId) },
        });
        
        var webappAKV = new KeyVault("webappAKV", new Pulumi.Azure.KeyVault.KeyVaultArgs
        {
            Location = resourceGroup.Location,
            ResourceGroupName = resourceGroup.Name,
            EnabledForDiskEncryption = true,
            TenantId = current.Apply(current => current.TenantId),
            SoftDeleteEnabled = true,
            SoftDeleteRetentionDays = 7,
            PurgeProtectionEnabled = false,
            SkuName = "standard",
            AccessPolicies =
            {
                new KeyVaultAccessPolicyArgs
                {
                    TenantId = current.Apply(current => current.TenantId),
                    ObjectId = current.Apply(current => current.ObjectId),
                    KeyPermissions =
                    {
                        "get",
                    },
                    SecretPermissions =
                    {
                        "set", "get", "list", "delete"
                    },
                    CertificatePermissions =
                    {
                        "get",
                    },
                },
                new KeyVaultAccessPolicyArgs
                {
                    TenantId = current.Apply(current => current.TenantId),
                    ObjectId =  appADGroup.ObjectId,
                    KeyPermissions =
                    {
                        "create",
                    },
                    SecretPermissions =
                    {
                        "set", "get", "list"
                    },
                    CertificatePermissions =
                    {
                        "get",
                    },
                },
            },
            Tags =
            {
                { "environment", "Testing" },
            },
        });

        var secret = new Secret("servicebuscs", new SecretArgs
        {
            KeyVaultId = webappAKV.Id,
            Value = serviceBus.DefaultPrimaryKey,
        });

        this.akvurl = webappAKV.VaultUri;
        this.secretURL = secret.Id;
    }
    [Output]
    public Output<string> akvurl { get; set; }
    [Output]
    public Output<string> secretURL { get; set; }
}
