# Third-Party Software Notices

Inventory Store incorporates the following open-source components.

---

## .NET Runtime and ASP.NET Core

**Copyright** (c) .NET Foundation and Contributors  
**License:** MIT  
https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt

---

## Entity Framework Core

**Copyright** (c) .NET Foundation and Contributors  
**License:** MIT  
https://github.com/dotnet/efcore/blob/main/LICENSE.txt

---

## Microsoft.Data.Sqlite

**Copyright** (c) .NET Foundation and Contributors  
**License:** MIT  
https://github.com/dotnet/efcore/blob/main/LICENSE.txt

---

## Microsoft.Extensions.Hosting.WindowsServices

**Copyright** (c) .NET Foundation and Contributors  
**License:** MIT  
https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

---

## Bootstrap 5

**Copyright** (c) 2011-2024 The Bootstrap Authors  
**License:** MIT  
https://github.com/twbs/bootstrap/blob/main/LICENSE

---

## Bootstrap Icons

**Copyright** (c) 2019-2024 The Bootstrap Authors  
**License:** MIT  
https://github.com/twbs/icons/blob/main/LICENSE

---

## @zxing/library (ZXing for JavaScript)

**Copyright** (c) ZXing authors  
**License:** Apache License 2.0  
https://github.com/zxing-js/library/blob/master/LICENSE

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

---

## JsBarcode

**Copyright** (c) Johan Lindell  
**License:** MIT  
https://github.com/lindell/JsBarcode/blob/master/MIT-LICENSE.txt

---

## cloudflared (Cloudflare Tunnel client)

**Copyright** (c) Cloudflare, Inc.  
**License:** Apache License 2.0  
https://github.com/cloudflare/cloudflared/blob/master/LICENSE

Cloudflared binaries are downloaded at runtime if the user enables a Cloudflare tunnel. They are not bundled with this installer.

---

## localtunnel

**Copyright** (c) Roman Shtylman and contributors  
**License:** MIT  
https://github.com/localtunnel/localtunnel/blob/master/LICENSE

LocalTunnel is invoked as a subprocess via `npx localtunnel` if the user enables a LocalTunnel. It is not bundled with this installer.

---

## SQLite

SQLite is in the public domain.  
https://www.sqlite.org/copyright.html

---

## GHS Hazard Pictograms

The Globally Harmonized System (GHS) hazard pictograms bundled in `wwwroot/img/ghs` are the standard
symbols published by the United Nations and are in the public domain.  
https://unece.org/transport/dangerous-goods/ghs-pictograms

---

## External Data Services

Inventory Store can query the following free public data services at runtime when you use the related features. These services are not bundled with the installer, and a request is only made when you perform a lookup.

- **PubChem** (U.S. National Institutes of Health / National Library of Medicine) - chemical safety data for the optional Safety Data Sheets module.  
  https://pubchem.ncbi.nlm.nih.gov/
- **Open Library** - book details for ISBN barcode lookups.  
  https://openlibrary.org/
- **UPC Item DB** - general retail product details for barcode lookups.  
  https://www.upcitemdb.com/
- **Open Food Facts** - food and beverage details for barcode lookups.  
  https://world.openfoodfacts.org/

---

*This list reflects dependencies known at the time of release. Transitive dependencies may introduce additional third-party components under their respective licenses.*
