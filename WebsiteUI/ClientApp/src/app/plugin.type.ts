export class VaultPlugin {
  Name: string;
  Description: string;
  VaultAccountId: string;
  Version: string;
  Configuration: any;
  IsLoaded: boolean;
  DisplayName: string;
  IsUploadCustom: boolean;
  IsConfigured: boolean;
  Accounts = [];

  constructor(name: string, description: string) {
    this.Name = name;
    this.Description = description;
  }
}
