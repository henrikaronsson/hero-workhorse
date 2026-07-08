// Container App + Key Vault + user-assigned Managed Identity for hosting an agent API.
// Secrets (e.g. Azure OpenAI key, SQL connection string) live in Key Vault and are
// referenced by the app through its identity — never baked into appsettings.
//
// Deploy:
//   az group create -n rg-agent -l westeurope
//   az deployment group create -g rg-agent -f container-app.bicep -p appName=my-agent containerImage=<acr>/<image>:<tag>

param appName string
param location string = resourceGroup().location
param containerImage string
@description('Name of the Key Vault secret holding the model provider API key.')
param apiKeySecretName string = 'openai-api-key'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${appName}-identity'
  location: location
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${appName}-kv'
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
  }
}

// Key Vault Secrets User for the app identity.
resource kvSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identity.id, 'kv-secrets-user')
  scope: keyVault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env'
  location: location
  properties: {}
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
      secrets: [
        {
          name: 'api-key'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${apiKeySecretName}'
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: containerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'OpenAI__ApiKey', secretRef: 'api-key' }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
  dependsOn: [kvSecretsUser]
}

output appUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output keyVaultName string = keyVault.name
