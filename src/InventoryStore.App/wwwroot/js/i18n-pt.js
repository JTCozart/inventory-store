(function () {
    if (!window.APP_CULTURE || !window.APP_CULTURE.startsWith('pt')) return;

    const exactMap = {
        // Navigation & Common
        "Dashboard": "Painel",
        "Inventory": "Inventário",
        "Clients": "Clientes",
        "Reports": "Relatórios",
        "Settings": "Configurações",
        "User Guide": "Guia do Usuário",
        "Terminal": "Terminal",
        "Quick Scan": "Leitura Rápida",
        "Toggle Theme": "Alternar Tema",
        "Toggle theme": "Alternar tema",
        "My Account": "Minha Conta",
        "Logout": "Sair",
        "Log out": "Sair",
        "User menu": "Menu do usuário",
        "Open menu": "Abrir menu",
        "Assign Consumables": "Atribuir Consumíveis",
        "Atribuir Consumíveis": "Atribuir Consumíveis",
        "Assign Consumable": "Atribuir Consumível",
        "Atribuir Consumível": "Atribuir Consumível",
        "Consumable Assignments": "Atribuição de Consumíveis",
        "Assigned Consumables": "Consumíveis Atribuídos",
        "Consumíveis Atribuídos": "Consumíveis Atribuídos",
        "Select client...": "Selecionar cliente...",
        "No consumables assigned.": "Nenhum consumível atribuído.",
        "Save": "Salvar",
        "Cancel": "Cancelar",
        "Close": "Fechar",
        "Edit": "Editar",
        "Delete": "Excluir",
        "Create": "Criar",
        "Add": "Adicionar",
        "Remove": "Remover",
        "Update": "Atualizar",
        "Apply": "Aplicar",
        "Search": "Buscar",
        "Filter": "Filtrar",
        "Clear": "Limpar",
        "Reset": "Redefinir",
        "Back": "Voltar",
        "Next": "Próximo",
        "Previous": "Anterior",
        "Yes": "Sim",
        "No": "Não",
        "OK": "OK",
        "Loading...": "Carregando...",
        "Loading…": "Carregando…",
        "Actions": "Ações",
        "Status": "Status",
        "Type": "Tipo",
        "Name": "Nome",
        "Description": "Descrição",
        "Notes": "Observações",
        "Optional": "Opcional",
        "(optional)": "(opcional)",
        "(required)": "(obrigatório)",
        "Required.": "Obrigatório.",
        "Name is required.": "O nome é obrigatório.",
        "Please enter a name.": "Por favor, insira um nome.",
        "None": "Nenhum",
        "All": "Todos",
        "Active": "Ativo",
        "Inactive": "Inativo",
        "Enabled": "Habilitado",
        "Disabled": "Desabilitado",
        "Never": "Nunca",
        "Not scheduled": "Não agendado",
        "Unknown": "Desconhecido",
        "Date": "Data",
        "Time": "Hora",
        "From": "De",
        "To": "Até",
        "Print": "Imprimir",
        "Export CSV": "Exportar CSV",
        "Export": "Exportar",
        "Import": "Importar",
        "Download": "Baixar",
        "Upload": "Enviar",

        // Auth / Login / Setup
        "Login": "Entrar",
        "Sign In": "Entrar",
        "Signing in…": "Entrando…",
        "Username": "Usuário",
        "Password": "Senha",
        "Confirm Password": "Confirmar Senha",
        "Current Password": "Senha Atual",
        "New Password": "Nova Senha",
        "Update Password": "Atualizar Senha",
        "Change Password": "Alterar Senha",
        "Forgot your password?": "Esqueceu sua senha?",
        "Reset Password": "Redefinir Senha",
        "Initial Setup": "Configuração Inicial",
        "Welcome to Inventory Store": "Bem-vindo ao Inventory Store",
        "Create an admin account to get started.": "Crie uma conta de administrador para começar.",
        "First Name": "Nome",
        "Last Name": "Sobrenome",
        "Email": "E-mail",
        "Time Zone": "Fuso Horário",
        "Time zone": "Fuso horário",
        "Use each viewer's device time zone": "Usar o fuso horário do dispositivo de cada usuário",
        "Times across the app are shown in this zone. You can change it later in Settings.": "Os horários no aplicativo são exibidos neste fuso. Você pode alterá-lo depois nas Configurações.",
        "Create Admin Account": "Criar Conta de Administrador",
        "Login failed. Please try again.": "Falha no login. Tente novamente.",
        "Network error. Please try again.": "Erro de rede. Tente novamente.",

        // Dashboard
        "Enter SKU / scan": "Digite o SKU / escaneie",
        "Scan": "Escanear",
        "Scan barcode": "Escanear código de barras",
        "Items": "Itens",
        "Total Items": "Total de Itens",
        "In Stock": "Em Estoque",
        "Low Stock": "Estoque Baixo",
        "Checked Out": "Em Uso / Retirado",
        "Available to Checkout": "Disponível para Retirada",
        "Filter by name or category…": "Filtrar por nome ou categoria…",
        "Could not load items.": "Não foi possível carregar os itens.",
        "No reusable items match the filter.": "Nenhum item reutilizável corresponde ao filtro.",
        "Add Item": "Adicionar Item",
        "Item": "Item",
        "Category": "Categoria",
        "Available": "Disponível",
        "Avail": "Disp.",
        "Min": "Mín.",
        "Min Qty": "Qtd. Mín.",
        "View report": "Ver relatório",
        "View low stock report": "Ver relatório de estoque baixo",
        "View checked out report": "Ver relatório de itens retirados",
        "All stock levels OK.": "Todos os níveis de estoque estão OK.",
        "No items currently checked out.": "Nenhum item retirado no momento.",
        "With": "Com",
        "Client": "Cliente",
        "Borrower": "Responsável / Cliente",
        "Qty": "Qtd.",
        "Quantity": "Quantidade",
        "Days": "Dias",
        "5 / page": "5 / pág.",
        "10 / page": "10 / pág.",
        "50 / page": "50 / pág.",
        "100 / page": "100 / pág.",

        // Inventory & Modals
        "Metadata": "Metadados",
        "Search by name, SKU, location, category...": "Buscar por nome, SKU, localização, categoria...",
        "Search name, SKU, location...": "Buscar nome, SKU, localização...",
        "All Categories": "Todas as Categorias",
        "All Types": "Todos os Tipos",
        "Low Stock Only": "Apenas Estoque Baixo",
        "No items found.": "Nenhum item encontrado.",
        "Add your first item.": "Adicione seu primeiro item.",
        "SKU": "SKU",
        "SKU / Barcode": "SKU / Código de Barras",
        "Location": "Localização",
        "Expiry": "Validade",
        "Expiry Date": "Data de Validade",
        "Reusable": "Reutilizável",
        "Consumable": "Consumível",
        "Kit": "Kit",
        "Expired": "Vencido",
        "Lost": "Perdido",
        "Out": "Em Uso",
        "Returned": "Devolvido",
        "General": "Geral",
        "Details": "Detalhes",
        "Safety": "Segurança",
        "Cost": "Custo",
        "Maintenance": "Manutenção",
        "Product Details": "Detalhes do Produto",
        "Active Checkouts": "Retiradas Ativas",
        "Currently Out": "Atualmente Retirado",
        "Currently out": "Atualmente retirado",
        "Lost Items": "Itens Perdidos",
        "Check Out": "Retirar",
        "Check In": "Devolver",
        "Return": "Devolver",
        "Search by name…": "Buscar por nome…",
        "Search name…": "Buscar nome…",
        "Adjust Stock": "Ajustar Estoque",
        "Adjust stock": "Ajustar estoque",
        "Consume (use stock)": "Consumir (usar estoque)",
        "Restock (add stock)": "Repor (adicionar estoque)",
        "Notes (optional)": "Observações (opcional)",
        "Tags": "Etiquetas",
        "View SDS Info": "Ver Informações FISPQ",
        "Safety Data Sheet Info": "Informações da Ficha de Segurança (FISPQ)",
        "Unit Cost": "Custo Unitário",
        "Line Value": "Valor Total",
        "Last Maintained": "Última Manutenção",
        "Next Due": "Próximo Vencimento",
        "Edit Item": "Editar Item",
        "New Item": "Novo Item",
        "Total Quantity": "Quantidade Total",
        "Minimum Quantity": "Quantidade Mínima",
        "(low-stock alert)": "(alerta de estoque baixo)",
        "Create new category": "Criar nova categoria",
        "Category name": "Nome da categoria",
        "Color": "Cor",
        "Allow checking out / using a partial kit when some items are short": "Permitir retirar / usar um kit parcial quando alguns itens estiverem em falta",
        "When off, the kit must be complete (every item in stock) to be checked out or used.": "Quando desativado, o kit deve estar completo (todos os itens em estoque) para ser retirado ou usado.",
        "Kit Items": "Itens do Kit",
        "Search items to add…": "Buscar itens para adicionar…",
        "Look up product by barcode": "Buscar produto por código de barras",
        "Product matched": "Produto vinculado",
        "(type to search, Enter to add)": "(digite para buscar, Enter para adicionar)",
        "Scan Warning": "Aviso ao Escanear",
        "e.g. Requires PPE, Handle with care": "ex: Requer EPI, Manuseie com cuidado",
        "Chemical name": "Nome químico",
        "(for SDS lookup)": "(para busca de FISPQ)",
        "e.g. Acetone": "ex: Acetona",
        "Look up SDS": "Buscar FISPQ",
        "Look up safety data from PubChem": "Buscar dados de segurança no PubChem",
        "Searches PubChem for GHS hazard data and attaches it to this item.": "Busca dados de perigo GHS no PubChem e os anexa a este item.",
        "Purchase Date": "Data de Compra",
        "Useful Life": "Vida Útil",
        "(months, reusables)": "(meses, reutilizáveis)",
        "Save Cost": "Salvar Custo",
        "Service Every": "Manutenção a cada",
        "Days": "Dias",
        "Months": "Meses",
        "Years": "Anos",
        "Set 0 for no recurring schedule.": "Defina 0 para sem agendamento recorrente.",
        "Maintenance Notes": "Observações de Manutenção",
        "Save Schedule": "Salvar Agendamento",
        "Product Lookup": "Busca de Produto",
        "Searching product databases...": "Buscando em bancos de dados de produtos...",
        "No products found for this barcode.": "Nenhum produto encontrado para este código de barras.",
        "Use This": "Usar Este",
        "Please enter or scan a barcode first.": "Por favor, digite ou escaneie um código de barras primeiro.",
        "Lookup failed. Please try again.": "Falha na busca. Tente novamente.",
        "Scan Item": "Escanear Item",
        "Type or scan…": "Digite ou escaneie…",
        "Click Start to activate camera": "Clique em Iniciar para ativar a câmera",
        "Start Camera": "Iniciar Câmera",
        "Point the camera at the barcode": "Aponte a câmera para o código de barras",
        "Stop": "Parar",
        "Stop camera": "Parar câmera",
        "Reading barcode…": "Lendo código de barras…",
        "Multiple items found — select one:": "Vários itens encontrados — selecione um:",
        "Add New Item Instead": "Adicionar Novo Item",
        "No item found.": "Nenhum item encontrado.",
        "Item name (required)": "Nome do item (obrigatório)",
        "SKU (optional)": "SKU (opcional)",
        "Location (optional)": "Localização (opcional)",
        "Add & Continue": "Adicionar e Continuar",
        "Scan Another Item": "Escanear Outro Item",
        "Brand": "Marca",
        "Size": "Tamanho",
        "Weight": "Peso",

        // Clients
        "Add Client": "Adicionar Cliente",
        "Edit Client": "Editar Cliente",
        "Delete Client": "Excluir Cliente",
        "Search by name or phone…": "Buscar por nome ou telefone…",
        "No clients found.": "Nenhum cliente encontrado.",
        "Phone": "Telefone",
        "Date of Birth": "Data de Nascimento",
        "Address": "Endereço",
        "Checkout History": "Histórico de Retiradas",
        "Failed to load client.": "Falha ao carregar cliente.",
        "Client saved.": "Cliente salvo.",
        "Client added.": "Cliente adicionado.",
        "Client deleted.": "Cliente excluído.",

        // Reports
        "Stock Levels": "Níveis de Estoque",
        "Take Inventory": "Balanço de Estoque",
        "Incomplete Kits": "Kits Incompletos",
        "Reconciliation": "Reconciliação",
        "Barcode Sheet": "Folha de Códigos de Barras",
        "Activity Log": "Registro de Atividades",
        "Valuation": "Avaliação de Custos",
        "Forecast": "Previsão",
        "Maintenance Due": "Manutenção Pendente",
        "Overdue": "Atrasado",
        "Due Soon": "Vence em Breve",
        "Due soon": "Vence em breve",
        "Out for Maintenance": "Em Manutenção",
        "Out for maintenance": "Em manutenção",
        "No items have a maintenance schedule or are out for service.": "Nenhum item possui cronograma de manutenção ou está em serviço.",
        "Vendor": "Fornecedor",

        // Settings
        "Users": "Usuários",
        "User Accounts": "Contas de Usuário",
        "Add User": "Adicionar Usuário",
        "Role": "Função / Perfil",
        "Last Login": "Último Acesso",
        "Network": "Rede",
        "Public View": "Visualização Pública",
        "Database": "Banco de Dados",
        "Notifications": "Notificações",
        "Modules": "Módulos",
        "Categories": "Categorias",
        "Locations": "Localizações",
        "Vendors": "Fornecedores",
        "App Info": "Informações do App",
        "About": "Sobre",
        "Admin": "Administrador",
        "Manager": "Gerente",
        "Viewer": "Visualizador",
        "Staff": "Equipe",
        "System": "Sistema",
        "Public Base URL": "URL Base Pública",
        "Public base URL": "URL base pública",
        "General Settings": "Configurações Gerais",
        "App Name": "Nome do Aplicativo",
        "Require Login": "Exigir Login",
        "Allow Registration": "Permitir Registro",
        "Default Currency": "Moeda Padrão",
        "Session Timeout": "Tempo Limite de Sessão",
        "Send Test Email": "Enviar E-mail de Teste",
        "Backup Database": "Fazer Backup do Banco",
        "Restore Database": "Restaurar Banco de Dados",
        "Download Backup": "Baixar Backup",
        "Upload Backup": "Enviar Backup",
        "Delete Account": "Excluir Conta",
        "Reset All Data": "Redefinir Todos os Dados",
        "API Keys": "Chaves de API",
        "Generate API Key": "Gerar Chave de API",
        "Revoke Key": "Revogar Chave",
        "Documentation": "Documentação",
        "Webhook Endpoints": "Endpoints de Webhook",
        "Add Webhook": "Adicionar Webhook",
        "Test Webhook": "Testar Webhook",
        "Event Subscriptions": "Inscrições de Eventos",
        "Create User": "Criar Usuário",
        "Edit User": "Editar Usuário",
        "Delete User": "Excluir Usuário",
        "Add Category": "Adicionar Categoria",
        "Edit Category": "Editar Categoria",
        "Delete Category": "Excluir Categoria",
        "Add Location": "Adicionar Localização",
        "Edit Location": "Editar Localização",
        "Delete Location": "Excluir Localização",
        "Add Tag": "Adicionar Etiqueta",
        "Edit Tag": "Editar Etiqueta",
        "Delete Tag": "Excluir Etiqueta",
        "Add Vendor": "Adicionar Fornecedor",
        "Edit Vendor": "Editar Fornecedor",
        "Delete Vendor": "Excluir Fornecedor",
        "Vendor Name": "Nome do Fornecedor",
        "Contact Person": "Pessoa de Contato",
        "Website": "Site",
        "Version": "Versão",
        "License": "Licença",
        "Source Code": "Código Fonte",
        "Report an Issue": "Reportar um Problema",
        "Third-Party Notices": "Avisos de Terceiros",
        "Check for Updates": "Verificar Atualizações",
        "Up to date": "Atualizado",
        "Update available": "Atualização disponível",
        "Theme": "Tema",
        "Light": "Claro",
        "Dark": "Escuro",

        // Auth & Passwords
        "Send reset link": "Enviar link de redefinição",
        "Username or email": "Usuário ou e-mail",
        "Back to sign in": "Voltar para o login",
        "Enter your username or email and we'll email you a link to reset your password.": "Digite seu usuário ou e-mail e enviaremos um link para redefinir sua senha.",
        "If that account exists and has an email on file, we've sent a password reset link to it.": "Se essa conta existir e possuir um e-mail cadastrado, enviamos um link de redefinição para ela.",
        "This link is invalid or has expired.": "Este link é inválido ou expirou.",
        "Request a new link": "Solicitar um novo link",
        "Reset password": "Redefinir senha",
        "Confirm New Password": "Confirmar Nova Senha",
        "Password updated successfully.": "Senha atualizada com sucesso.",
        "Incorrect current password.": "Senha atual incorreta.",
        "Passwords do not match.": "As senhas não coincidem.",
        "Password must be at least 8 characters.": "A senha deve ter no mínimo 8 caracteres.",

        // Inventory & Edit
        "Item Type": "Tipo de Item",
        "Item type cannot be changed after creation.": "O tipo do item não pode ser alterado após a criação.",
        "Lookup product metadata": "Buscar metadados do produto",
        "Delete Item": "Excluir Item",
        "Are you sure you want to delete this item?": "Tem certeza de que deseja excluir este item?",
        "This will permanently delete this item and its history.": "Isso excluirá permanentemente este item e seu histórico.",
        "Save Item": "Salvar Item",
        "Save Changes": "Salvar Alterações",
        "Create Item": "Criar Item",
        "Item saved successfully.": "Item salvo com sucesso.",
        "Item added successfully.": "Item adicionado com sucesso.",
        "Item deleted.": "Item excluído.",

        // Activity Log Actions
        "CheckOut": "Retirada",
        "CheckIn": "Devolução",
        "Lost": "Perdido",
        "Found": "Encontrado",
        "Consume": "Consumo",
        "Restock": "Reposição",
        "Created": "Criação",
        "Updated": "Atualização",
        "Deleted": "Exclusão",
        "LocationRenamed": "Local Renomeado",
        "LocationChanged": "Local Alterado",
        "CategoryChanged": "Categoria Alterada",
        "TagAdded": "Etiqueta Adicionada",
        "TagRemoved": "Etiqueta Removida",
        "LocationCleared": "Local Limpo",
        "TypeConverted": "Tipo Convertido",
        "KitComponentLinked": "Componente Vinculado",
        "KitComponentUnlinked": "Componente Desvinculado",
        "KitComponentQuantity": "Qtd. Componente",
        "KitCheckOut": "Retirada de Kit",
        "KitCheckIn": "Devolução de Kit",
        "KitReconcile": "Reconciliação Kit",
        "KitLost": "Kit Perdido",
        "MaintenanceSchedule": "Agendamento Manutenção",
        "MaintenanceOut": "Envio Manutenção",
        "MaintenanceReturn": "Retorno Manutenção",
        "UserLogin": "Login",
        "UserLoginPasskey": "Login Chave de Acesso",
        "UserLogout": "Logout",
        "PasswordResetRequested": "Solicitação Redef. Senha",
        "PasswordResetCompleted": "Senha Redefinida",
        "PasswordChanged": "Senha Alterada",
        "UserCreated": "Usuário Criado",
        "UserUpdated": "Usuário Atualizado",
        "UserDeleted": "Usuário Excluído",

        // Activity Log Static Details
        "User logged in": "Usuário conectou-se",
        "User logged in via passkey": "Usuário conectou-se via chave de acesso",
        "User logged out": "Usuário desconectou-se",
        "Password reset link emailed": "Link de redefinição de senha enviado por e-mail",
        "Password reset via emailed link": "Senha redefinida via link de e-mail",
        "Setup completed - admin user created": "Configuração inicial concluída - administrador criado",
        "Password reset. You can now sign in.": "Senha redefinida. Agora você pode entrar.",

        // Terminal
        "Scan or type SKU or name…": "Escaneie ou digite o SKU ou nome…",
        "Scan with camera": "Escanear com a câmera",
        "Go to main app": "Ir para o aplicativo principal",
        "Next item": "Próximo item",
        "Try again": "Tentar novamente",
        "Select an item": "Selecione um item",
        "Out of stock": "Sem estoque",
        "Out of Stock": "Sem Estoque",
        "Low stock": "Estoque baixo",
        "No complete kits available right now.": "Nenhum kit completo disponível no momento.",
        "Out of stock — no units are available to check out.": "Sem estoque — nenhuma unidade disponível para retirada.",
        "Out of stock — there is nothing left to use.": "Sem estoque — não há itens restantes para uso.",
        "Enter a borrower name first.": "Informe o nome do responsável primeiro.",
        "Checked in.": "Devolvido com sucesso.",
        "Skip for now": "Pular por enquanto",
        "Confirm & check in": "Confirmar e devolver",
        "How many came back unused?": "Quantos voltaram sem uso?",
        "How many did you use?": "Quantos foram utilizados?",
        "Kits out": "Kits retirados",
        "Some items are short:": "Alguns itens estão em falta:",
        "This kit must be complete.": "Este kit deve estar completo.",
        "Use what's available": "Usar o que estiver disponível",
        "Camera needs HTTPS. Type the SKU instead.": "A câmera requer HTTPS. Digite o SKU.",
        "This kit has no items.": "Este kit não possui itens.",
        "Check Out Kit": "Retirar Kit",
        "Number of kits": "Número de kits",

        // Reports & Forecasting
        "Total value": "Valor total",
        "Book value": "Valor contábil",
        "Depreciation": "Depreciação",
        "Avg/Day": "Média/Dia",
        "Days Left": "Dias Restantes",
        "Projected Run-out": "Esgotamento Projetado",
        "All clear: no items currently checked out.": "Tudo certo: nenhum item retirado no momento.",
        "All clear": "Tudo certo",
        "No items have SKUs assigned.": "Nenhum item possui SKU atribuído.",
        "Add SKUs to inventory items": "Adicione SKUs aos itens do inventário",
        "to generate barcodes.": "para gerar códigos de barras.",
        "Print this page and walk the warehouse to confirm quantities.": "Imprima esta página e percorra o estoque para conferir as quantidades.",
        "Actual Count": "Contagem Real",
        "Expected": "Esperado",
        "Kits that can't be assembled right now. Each one is missing stock for at least one of its items (or has no items yet).": "Kits que não podem ser montados agora. Falta estoque para pelo menos um de seus itens (ou ainda não têm itens).",
        "All kits can be assembled. Nothing is incomplete.": "Todos os kits podem ser montados. Nenhum está incompleto.",
        "Buildable": "Montáveis",
        "Missing Items": "Itens Faltantes",
        "Items Short": "Itens em Falta",
        "No items linked to this kit yet.": "Nenhum item vinculado a este kit ainda.",
        "Nothing to reconcile. Every checked-in kit has been accounted for.": "Nada a reconciliar. Todos os kits devolvidos foram conferidos.",
        "Reconcile kit consumables": "Reconciliar consumíveis do kit",
        "For each item, count how many came back unused. The leftover is returned to stock.": "Para cada item, conte quantos voltaram sem uso. A sobra é devolvida ao estoque.",
        "Confirm": "Confirmar",
        "Confirm & check in": "Confirmar e devolver",
        "Enter how many of each item came back unused.": "Informe quantos itens voltaram sem uso.",
        "No consumption recorded yet in the forecast window.": "Nenhum consumo registrado ainda na janela de previsão.",
        "No item costs recorded yet.": "Nenhum custo de item registrado ainda.",
        "Stock Levels Report": "Relatório de Níveis de Estoque",
        "Maintenance Due Report": "Relatório de Manutenção Pendente",
        "Checked Out Items": "Itens Retirados",
        "Lost Items Report": "Relatório de Itens Perdidos",
        "Expiry Report": "Relatório de Validade",
        "Date Lost": "Data da Perda",
        "Date Out": "Data de Saída",
        "Days Out": "Dias Fora",
        "Last With": "Último com",
        "Checked Out By": "Retirado Por",
        "Checked In": "Devolvido",
        "Consumables": "Consumíveis",
        "Action": "Ação",
        "Reconcile": "Reconciliar",
        "Overdue Only (>7 days)": "Apenas Atrasados (>7 dias)",
        "Search item or person...": "Buscar item ou pessoa...",
        "Search user or details...": "Buscar usuário ou detalhes...",
        "All Actions": "Todas as Ações",
        "All Locations": "Todas as Localizações",
        "Showing": "Exibindo",
        "Recent Activity": "Atividade Recente",
        "Full log": "Registro completo",
        "No activity recorded yet.": "Nenhuma atividade registrada ainda."
        "Warning:": "Aviso:",
        "Action completed.": "Ação concluída.",
        "Action failed.": "Falha na ação.",
        "Changes saved.": "Alterações salvas.",
        "Check in failed.": "Falha na devolução.",
        "Check out failed.": "Falha na retirada.",
        "Checked in successfully.": "Devolvido com sucesso.",
        "Checked out successfully.": "Retirado com sucesso.",
        "Consume failed.": "Falha ao consumir estoque.",
        "Restock failed.": "Falha ao repor estoque.",
        "Conversion failed.": "Falha na conversão.",
        "Could not add item to kit.": "Não foi possível adicionar o item ao kit.",
        "Could not remove item.": "Não foi possível remover o item.",
        "Could not update quantity.": "Não foi possível atualizar a quantidade.",
        "Could not read barcode — try again with the barcode clearly in frame.": "Não foi possível ler o código de barras — tente novamente com o código nítido no quadro.",
        "Failed to add item.": "Falha ao adicionar item.",
        "Failed to create client.": "Falha ao criar cliente.",
        "Item marked as lost.": "Item marcado como perdido.",
        "Item not found": "Item não encontrado",
        "Item not found.": "Item não encontrado.",
        "Mark this item as lost?": "Marcar este item como perdido?",
        "No barcode to lookup": "Nenhum código de barras para buscar",
        "Not available": "Não disponível",
        "This item is currently out for maintenance.": "Este item está atualmente em manutenção.",
        "Tap to take a photo of the barcode": "Toque para tirar uma foto do código de barras",
        "Search name or SKU...": "Buscar nome ou SKU...",
        "Search vendors…": "Buscar fornecedores…",
        "Scan or type": "Escanear ou digitar",
        "Clear from all items": "Remover de todos os itens",
        "Copy URL": "Copiar URL",
        "Copy public key": "Copiar chave pública",
        "Leave blank for unsigned": "Deixe em branco para não assinado",
        "Leave blank to keep current": "Deixe em branco para manter o atual",
        "Leave blank to keep current token": "Deixe em branco para manter o token atual",
        "Leave blank to keep unchanged": "Deixe em branco para manter inalterado",
        "Lookup barcode": "Buscar código de barras",
        "Manage items": "Gerenciar itens",
        "Open in PubChem": "Abrir no PubChem",
        "Protected system account": "Conta do sistema protegida",
        "Refresh from source": "Atualizar da fonte",
        "Reload list": "Recarregar lista",
        "Rotate: issue a new key": "Rotacionar: emitir uma nova chave",
        "Toggle public visibility": "Alternar visibilidade pública",
        "What is this key for? e.g. Kitchen scale": "Para que serve esta chave? ex: Balança da cozinha",
        "e.g. GitHub, a friend, a search": "ex: GitHub, um amigo, uma busca",
        "e.g. Requires PPE, Handle with care, Check expiry date": "ex: Requer EPI, Manuseie com cuidado, Verifique a validade",
        "e.g. Shelf A3, Bin 12": "ex: Prateleira A3, Gaveta 12",
        "Chemical name, e.g. Acetone": "Nome químico, ex: Acetona",
        "Initial admin account created": "Conta de administrador inicial criada",
        "Admin password reset via system tray": "Senha de admin redefinida via bandeja do sistema",
        "Are you using Inventory Store?": "Você está usando o Inventory Store?",
        "How did you hear about the app?": "Como você conheceu o aplicativo?",
        "No thanks, do not ask again": "Não, obrigado, não pergunte novamente",
        "The AI Assistant needs an NVIDIA API key before it can answer questions.": "O Assistente de IA precisa de uma chave de API da NVIDIA para responder perguntas.",
        "Configure in Settings": "Configurar nas Configurações",
        "Ask about your inventory…": "Pergunte sobre seu estoque…",
        "Ask the AI Assistant": "Perguntar ao Assistente de IA",
        "New conversation": "Nova conversa",
        "Ready": "Pronto",
        "Something went wrong": "Algo deu errado",
        "An unexpected error occurred. Please try again or contact your administrator.": "Ocorreu um erro inesperado. Tente novamente ou entre em contato com o administrador.",
        "Request ID:": "ID da Requisição:",
        "Go to Dashboard": "Ir para o Painel",
        "Inventory – Public View": "Inventário – Visualização Pública",
        "Public view is not enabled.": "A visualização pública não está ativada.",
        "An administrator can enable it in Settings.": "Um administrador pode ativá-la nas Configurações.",
        "No items are marked as public yet.": "Nenhum item está marcado como público ainda.",
        "Uncategorized": "Sem Categoria",
        "Managed by the system administrator on this hosted instance.": "Gerenciado pelo administrador do sistema nesta instância hospedada.",
        "Set this if the app is reachable over the internet": "Defina isso se o aplicativo for acessível pela internet",
        "Suspended": "Suspenso",
        "Protected": "Protegido",
        "Staff (Terminal)": "Equipe (Terminal)",
        "Active (uncheck to suspend)": "Ativo (desmarque para suspender)",
        "Network settings are disabled when in a hosted environment.": "As configurações de rede estão desativadas em um ambiente hospedado.",
        "Listening port": "Porta de escuta",
        "Redirect to TLS": "Redirecionar para TLS",
        "Remote Access": "Acesso Remoto",
        "Public URL": "URL Pública",
        "Quick Tunnel": "Túnel Rápido",
        "random URL": "URL aleatória",
        "Stop Tunnel": "Parar Túnel",
        "Tunnels provide HTTPS remote access and enable the camera barcode scanner from any device.": "Os túneis fornecem acesso remoto HTTPS e habilitam o leitor de código de barras por câmera em qualquer dispositivo.",
        "free persistent subdomain": "subdomínio persistente gratuito",
        "Configured": "Configurado",
        "Generate your SSH key": "Gere sua chave SSH",
        "The app generates a unique SSH key pair. The private key is stored securely in the app database. You only do this once.": "O aplicativo gera um par exclusivo de chaves SSH. A chave privada é armazenada com segurança no banco de dados. Você só precisa fazer isso uma vez.",
        "Generate SSH Key": "Gerar Chave SSH",
        "SSH key generated. Copy the public key below:": "Chave SSH gerada. Copie a chave pública abaixo:",
        "Key ready (RSA 4096)": "Chave pronta (RSA 4096)",
        "Choose your subdomain": "Escolha seu subdomínio",
        "Pick a name — once your SSH key is registered it's permanently yours:": "Escolha um nome — assim que sua chave SSH for registrada, ele será seu permanentemente:",
        "Save Subdomain": "Salvar Subdomínio",
        "No account needed. Subdomain is first-come-first-served.": "Nenhuma conta necessária. Subdomínio por ordem de chegada.",
        "First-time visitors need to bypass the loca.lt landing page once.": "Visitantes de primeira viagem precisam passar pela página inicial do loca.lt uma vez.",
        "Cert uploaded, disabled": "Certificado enviado, desabilitado",
        "Automatic certificate (Let's Encrypt)": "Certificado automático (Let's Encrypt)",
        "Enable automatic HTTPS": "Habilitar HTTPS automático",
        "Domain": "Domínio",
        "I accept the": "Eu aceito os",
        "Let's Encrypt Terms of Service": "Termos de Serviço do Let's Encrypt",
        "Or upload your own certificate": "Ou envie seu próprio certificado",
        "Enable HTTPS": "Habilitar HTTPS",
        "Port": "Porta",
        "Certificate uploaded": "Certificado enviado",
        "Upload a new .pfx to replace the existing certificate.": "Envie um novo .pfx para substituir o certificado existente.",
        "Certificate Password": "Senha do Certificado",
        "Remove Certificate": "Remover Certificado",
        "Restart Service": "Reiniciar Serviço",
        "Auto-start": "Início automático",
        "No categories yet. Add one to start organizing your inventory.": "Nenhuma categoria ainda. Adicione uma para começar a organizar seu estoque.",
        "Currently in this category": "Atualmente nesta categoria",
        "Add an item to this category": "Adicionar um item a esta categoria",
        "No tags yet. Add one, or create tags inline while editing an item.": "Nenhuma etiqueta ainda. Adicione uma ou crie ao editar um item.",
        "Tag": "Etiqueta",
        "Items with this tag": "Itens com esta etiqueta",
        "Add this tag to an item": "Adicionar esta etiqueta a um item",
        "Tags are coloured automatically from their name.": "As etiquetas são coloridas automaticamente a partir de seu nome.",
        "Rename Tag": "Renomear Etiqueta",
        "No locations have been assigned to any inventory items yet.": "Nenhuma localização foi atribuída a itens do inventário ainda.",
        "Location Name": "Nome da Localização",
        "After naming it, you will be taken to assign items to this location.": "Após nomeá-la, você poderá atribuir itens a esta localização.",
        "Rename Location": "Renomear Localização",
        "Current name": "Nome atual",
        "New name": "Novo nome",
        "All items currently at this location will be updated.": "Todos os itens atualmente nesta localização serão atualizados.",
        "Rename": "Renomear",
        "Currently here": "Atualmente aqui",
        "Add an item to this location": "Adicionar um item a esta localização",
        "No items at this location.": "Nenhum item nesta localização.",
        "All items are already at this location": "Todos os itens já estão nesta localização",
        "Enable public view": "Habilitar visualização pública",
        "Embed Code": "Código de Incorporação",
        "Copy this tag into any web page. Use your tunnel URL for external access.": "Copie esta tag em qualquer página web. Use a URL do seu túnel para acesso externo.",
        "Direct link:": "Link direto:",
        "Tunnel:": "Túnel:",
        "Visible Items": "Itens Visíveis",
        "Show": "Exibir",
        "Backup": "Backup",
        "Downloads a consistent snapshot of the current database as a": "Baixa um instantâneo consistente do banco de dados atual como um",
        "Restore from Backup": "Restaurar do Backup",
        "This replaces all current data.": "Isso substitui todos os dados atuais.",
        "Download a backup first. This action cannot be undone.": "Baixe um backup primeiro. Esta ação não pode ser desfeita.",
        "Backup File": "Arquivo de Backup",
        "ntfy Push Notifications": "Notificações Push ntfy",
        "Send push notifications via": "Enviar notificações push via",
        "or a self-hosted ntfy server. Leave Topic blank to disable all notifications.": "ou um servidor ntfy auto-hospedado. Deixe o Tópico em branco para desabilitar todas as notificações.",
        "Server URL": "URL do Servidor",
        "Topic": "Tópico",
        "Auth Token": "Token de Autenticação",
        "Token is set.": "O token está definido.",
        "Clear token": "Limpar token",
        "Events": "Eventos",
        "Choose which events send a notification.": "Escolha quais eventos enviam notificação.",
        "Item checked out": "Item retirado",
        "Item returned (checked in)": "Item devolvido",
        "Item marked lost": "Item marcado como perdido",
        "Low stock alert (after consume)": "Alerta de estoque baixo (após consumo)",
        "User login": "Login de usuário",
        "Send Test": "Enviar Teste",
        "Application Info": "Informações do Aplicativo",
        "Local URL": "URL Local",
        "Network URL": "URL de Rede",
        "Maintenance Vendors": "Fornecedores de Manutenção",
        "Organizations that service items sent out for maintenance.": "Organizações que realizam manutenção em itens enviados para serviço.",
        "No vendors yet.": "Nenhum fornecedor ainda.",
        "Failed to load vendors.": "Falha ao carregar fornecedores.",
        "Enable to configure": "Habilite para configurar",
        "The Safety Data Sheets module is disabled.": "O módulo de Fichas de Segurança (FISPQ) está desabilitado.",
        "Enable it": "Habilite-o",
        "to manage metadata.": "para gerenciar metadados.",
        "Safety Data Sheets — Metadata": "Fichas de Segurança (FISPQ) — Metadados",
        "Safety data attached to inventory items. Deleting a row removes the SDS info from that item.": "Dados de segurança anexados a itens do estoque. Excluir uma linha remove as informações de FISPQ desse item.",
        "No safety data sheets yet. Attach one from an item's edit panel.": "Nenhuma ficha de segurança (FISPQ) ainda. Anexe uma pelo painel de edição do item.",
        "Chemical": "Produto Químico",
        "CAS": "CAS",
        "Signal": "Palavra de Advertência",
        "Hazards": "Perigos",
        "Source": "Fonte",
        "Fetched": "Obtido em",
        "to configure.": "para configurar.",
        "Currency symbol": "Símbolo da moeda",
        "Valuation summary": "Resumo de avaliação de custos",
        "No item costs recorded yet. Add a unit cost on an item's edit panel.": "Nenhum custo registrado ainda. Adicione o custo unitário no painel de edição do item.",
        "Book Value": "Valor Contábil",
        "The Maintenance module is disabled.": "O módulo de Manutenção está desabilitado.",
        "Manage Vendors": "Gerenciar Fornecedores",
        "The Consumption Forecasting module is disabled.": "O módulo de Previsão de Consumo está desabilitado.",
        "Consumption Forecasting": "Previsão de Consumo",
        "Usage window (days)": "Janela de uso (dias)",
        "Run-out dates are projected from average daily use over this many recent days.": "As datas de esgotamento são projetadas a partir do uso diário médio nestes últimos dias.",
        "No consumption recorded yet in this window.": "Nenhum consumo registrado ainda nesta janela.",
        "Reconcile consumables at check-in": "Reconciliar consumíveis na devolução",
        "Used": "Utilizado",
        "Remaining": "Restante",
        "Reconciling a skipped kit later always counts what remains.": "A reconciliação de um kit pulado posteriormente sempre conta o que sobrou.",
        "The AI Assistant module is disabled.": "O módulo Assistente de IA está desabilitado.",
        "AI Assistant": "Assistente de IA",
        "Uses an NVIDIA-hosted AI model": "Usa um modelo de IA hospedado na NVIDIA",
        "NVIDIA API key": "Chave de API da NVIDIA",
        "An API key is configured. Leave blank to keep it.": "Uma chave de API está configurada. Deixe em branco para mantê-la.",
        "Model": "Modelo",
        "The Email Delivery module is disabled.": "O módulo de Envio de E-mail está desabilitado.",
        "Email delivery is managed by the system administrator on this hosted instance.": "O envio de e-mails é gerenciado pelo administrador nesta instância.",
        "Email Delivery": "Envio de E-mail",
        "Sends password-reset links through": "Envia links de redefinição de senha através de",
        "Mailjet API key": "Chave de API do Mailjet",
        "Mailjet secret key": "Chave secreta do Mailjet",
        "Mailjet credentials are configured. Leave blank to keep them.": "Credenciais do Mailjet configuradas. Deixe em branco para mantê-las.",
        "From address": "E-mail do remetente",
        "Must be a verified sender in your Mailjet account.": "Deve ser um remetente verificado na sua conta Mailjet.",
        "From name": "Nome do remetente",
        "Send test email": "Enviar e-mail de teste",
        "Send test": "Enviar teste",
        "The Webhooks module is disabled.": "O módulo de Webhooks está desabilitado.",
        "Add endpoint": "Adicionar endpoint",
        "Payload URL": "URL do Payload",
        "Shared secret": "Segredo compartilhado",
        "Endpoints": "Endpoints",
        "No endpoints yet.": "Nenhum endpoint ainda.",
        "URL": "URL",
        "Signed": "Assinado",
        "Last status": "Último status",
        "The Public API module is disabled.": "O módulo de API Pública está desabilitado.",
        "Public API": "API Pública",
        "Base address:": "Endereço base:",
        "Open API documentation": "Abrir documentação da API",
        "Create key": "Criar chave",
        "New key": "Nova chave",
        "Copy it now. For security it is not stored in a readable form and will not be shown again.": "Copie-a agora. Por segurança ela não é armazenada de forma legível e não será exibida novamente.",
        "Copy": "Copiar",
        "Keys": "Chaves",
        "No keys yet.": "Nenhuma chave ainda.",
        "Key": "Chave",
        "Last used": "Último uso",
        "Book value (after depreciation)": "Valor contábil (após depreciação)",
        "Items costed": "Itens avaliados",
        "never": "nunca",
        "no": "não",
        "Enter a URL.": "Digite uma URL.",
        "Pick at least one event.": "Selecione pelo menos um evento.",
        "Add failed.": "Falha ao adicionar.",
        "Network error.": "Erro de rede.",
        "Enter a name.": "Digite um nome.",
        "No items in this category.": "Nenhum item nesta categoria.",
        "All items are already in this category": "Todos os itens já estão nesta categoria",
        "No items have this tag.": "Nenhum item possui esta etiqueta.",
        "All items already have this tag": "Todos os itens já possuem esta etiqueta",
        "Clear filters": "Limpar filtros",
        "Import from CSV": "Importar do CSV",
        "CSV columns (header row required):": "Colunas do CSV (linha de cabeçalho obrigatória):",
        "CSV File": "Arquivo CSV",
        "Add Inventory Item": "Adicionar Item ao Estoque",
        "Stock is depleted when used": "O estoque é consumido quando utilizado",
        "Checked out and returned": "Retirado e devolvido",
        "Bundles other items together": "Reúne outros itens em conjunto",
        "Save the kit first, then add items to it from its Edit screen.": "Salve o kit primeiro, depois adicione itens a ele pela tela de Edição.",
        "Lookup Product": "Buscar Produto",
        "Safety Data Sheet": "Ficha de Segurança (FISPQ)",
        "Product Metadata": "Metadados do Produto",
        "Cached product info fetched from barcode lookups.": "Informações em cache obtidas de buscas por código de barras.",
        "Refresh": "Atualizar",
        "No stored metadata yet. It is created automatically when you look up a barcode.": "Nenhum metadado armazenado ainda. É criado automaticamente ao buscar um código de barras.",
        "Barcode": "Código de Barras",
        "Linked": "Vinculado",
        "Inventory Valuation": "Avaliação de Estoque",
        "Consumption Forecast": "Previsão de Consumo",
        "No lost items recorded.": "Nenhum item perdido registrado.",
        "No expired items.": "Nenhum item vencido.",
        "Expires": "Vence",
        "User": "Usuário",
        "No consumables to reconcile.": "Nenhum consumível a reconciliar.",
        "Enter a chemical name first.": "Informe o nome químico primeiro.",
        "Lookup failed.": "Falha na busca.",
        "Did you mean:": "Você quis dizer:",
        "Try a chemical name rather than a brand or product name.": "Tente um nome químico em vez de uma marca ou nome comercial.",
        "Found. Will attach when you save.": "Encontrado. Será anexado quando você salvar.",
        "Tap": "Toque em",
        "to consume stock,": "para consumir estoque,",
        "to add stock.": "para repor estoque.",
        "to consume,": "para consumir,",
        "to use stock": "para usar estoque",
        ", + to add stock": ", + para adicionar estoque",
        "re-fetches from the source API;": "busca novamente da API de origem;",
        "unlinks it from any items and removes it from storage.": "desvincula-o de quaisquer itens e o remove do armazenamento.",
        "Maintenance Due Report -": "Relatório de Manutenção Pendente -",
        "Stock Levels Report -": "Relatório de Níveis de Estoque -",
        "Checked Out Items -": "Itens Retirados -",
        "Lost Items Report -": "Relatório de Itens Perdidos -",
        "Take Inventory -": "Balanço de Estoque -",
        "Incomplete Kits -": "Kits Incompletos -",
        "Kit Reconciliation -": "Reconciliação de Kits -",
        "Barcode Sheet -": "Folha de Códigos de Barras -",
        "Activity Log -": "Registro de Atividades -",
    };

    const attrList = ['placeholder', 'title', 'aria-label'];

    function translateText(text) {
        if (!text) return null;
        const trimmed = text.trim();
        if (!trimmed) return null;
        if (exactMap[trimmed]) {
            return text.replace(trimmed, exactMap[trimmed]);
        }
        // Dynamic patterns & Activity logs
        let m;
        // Activity logs:

        // Auth, users & client assignments
        if ((m = trimmed.match(/^User\s+'(.+?)'\s+created$/i))) {
            return text.replace(trimmed, `Usuário '${m[1]}' criado`);
        }
        if ((m = trimmed.match(/^User\s+'(.+?)'\s+(suspended|reactivated)$/i))) {
            return text.replace(trimmed, `Usuário '${m[1]}' ${m[2].toLowerCase() === 'suspended' ? 'suspenso' : 'reativado'}`);
        }
        if ((m = trimmed.match(/^Password\s+reset\s+for\s+user\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Senha redefinida para o usuário '${m[1]}'`);
        }
        if ((m = trimmed.match(/^Assigned\s+(\d+)x\s+consumable\s+'(.+?)'\s+to\s+client\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Atribuído ${m[1]}x consumível '${m[2]}' ao cliente '${m[3]}'`);
        }
        if ((m = trimmed.match(/^Removed\s+assignment\s+of\s+consumable\s+'(.+?)'\s+from\s+client\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Removida a atribuição do consumível '${m[1]}' do cliente '${m[2]}'`);
        }
        if ((m = trimmed.match(/^Maintenance\s+due\s+soon\s+—\s+service\s+is\s+due\s+(.+)$/i))) {
            return text.replace(trimmed, `Manutenção próxima — serviço vence ${m[1]}`);
        }
        if ((m = trimmed.match(/^Maintenance\s+overdue\s+—\s+service\s+was\s+due\s+(.+)$/i))) {
            return text.replace(trimmed, `Manutenção atrasada — serviço venceu ${m[1]}`);
        }
        if ((m = trimmed.match(/^Next\s+due:\s*(.+)$/i))) {
            return text.replace(trimmed, `Próximo vencimento: ${m[1]}`);
        }
        if ((m = trimmed.match(/^Edit\s+User\s*—\s*(.+)$/i))) {
            return text.replace(trimmed, `Editar Usuário — ${m[1]}`);
        }
        if ((m = trimmed.match(/^Items\s+in\s+(.+)$/i))) {
            return text.replace(trimmed, `Itens em ${m[1]}`);
        }
        if ((m = trimmed.match(/^Items\s+tagged\s+(.+)$/i))) {
            return text.replace(trimmed, `Itens com a etiqueta ${m[1]}`);
        }
        if ((m = trimmed.match(/^Items\s+at\s+(.+)$/i))) {
            return text.replace(trimmed, `Itens em ${m[1]}`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+checked\s+out\s+(\d+)x\s+kit\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `'${m[1]}' retirou ${m[2]}x o kit '${m[3]}'`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+checked\s+out\s+(\d+)x\s+'(.+?)'(?::\s*(.*))?$/i))) {
            const notes = m[4] ? `: ${m[4]}` : '';
            return text.replace(trimmed, `'${m[1]}' retirou ${m[2]}x '${m[3]}'${notes}`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+checked\s+in\s+(\d+)x\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `'${m[1]}' devolveu ${m[2]}x '${m[3]}'`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+checked\s+in\s+kit\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `'${m[1]}' devolveu o kit '${m[2]}'`);
        }
        if ((m = trimmed.match(/^(\d+)x\s+'(.+?)'\s+marked\s+as\s+lost\s+\(checked\s+out\s+by\s+'(.+?)'\)$/i))) {
            return text.replace(trimmed, `${m[1]}x '${m[2]}' marcado como perdido (retirado por '${m[3]}')`);
        }
        if ((m = trimmed.match(/^(\d+)x\s+'(.+?)'\s+marked\s+as\s+found\s+\(was\s+checked\s+out\s+by\s+'(.+?)'\)$/i))) {
            return text.replace(trimmed, `${m[1]}x '${m[2]}' marcado como encontrado (estava retirado por '${m[3]}')`);
        }
        if ((m = trimmed.match(/^Kit\s+'(.+?)'\s+\(checked\s+out\s+by\s+'(.+?)'\)\s+marked\s+as\s+lost$/i))) {
            return text.replace(trimmed, `Kit '${m[1]}' (retirado por '${m[2]}') marcado como perdido`);
        }
        if ((m = trimmed.match(/^Consumed\s+(\d+)x\s+'(.+?)'(?::\s*(.*))?$/i))) {
            const notes = m[3] ? `: ${m[3]}` : '';
            return text.replace(trimmed, `Consumido ${m[1]}x '${m[2]}'${notes}`);
        }
        if ((m = trimmed.match(/^Restocked\s+(\d+)x\s+'(.+?)'(?::\s*(.*))?$/i))) {
            const notes = m[3] ? `: ${m[3]}` : '';
            return text.replace(trimmed, `Reposto ${m[1]}x '${m[2]}'${notes}`);
        }
        if ((m = trimmed.match(/^Created\s+(Reusable|Consumable|Kit)\s+item\s+'(.+?)'$/i))) {
            const typePt = m[1].toLowerCase() === 'reusable' ? 'reutilizável' : m[1].toLowerCase() === 'consumable' ? 'consumível' : 'kit';
            return text.replace(trimmed, `Criado item ${typePt} '${m[2]}'`);
        }
        if ((m = trimmed.match(/^Updated\s+item\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Item '${m[1]}' atualizado`);
        }
        if ((m = trimmed.match(/^Deleted\s+item\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Item '${m[1]}' excluído`);
        }
        if ((m = trimmed.match(/^Location\s+'(.+?)'\s+renamed\s+to\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Localização '${m[1]}' renomeada para '${m[2]}'`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+location\s+changed\s+from\s+'(.+?)'\s+to\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Localização de '${m[1]}' alterada de '${m[2]}' para '${m[3]}'`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+category\s+changed\s+from\s+'(.+?)'\s+to\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Categoria de '${m[1]}' alterada de '${m[2]}' para '${m[3]}'`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+tag\s+'(.+?)'\s+added$/i))) {
            return text.replace(trimmed, `Etiqueta '${m[2]}' adicionada a '${m[1]}'`);
        }
        if ((m = trimmed.match(/^'(.+?)'\s+tag\s+'(.+?)'\s+removed$/i))) {
            return text.replace(trimmed, `Etiqueta '${m[2]}' removida de '${m[1]}'`);
        }
        if ((m = trimmed.match(/^Location\s+'(.+?)'\s+cleared\s+from\s+all\s+items$/i))) {
            return text.replace(trimmed, `Localização '${m[1]}' removida de todos os itens`);
        }
        if ((m = trimmed.match(/^Converted\s+'(.+?)'\s+from\s+(\w+)\s+to\s+(\w+)$/i))) {
            return text.replace(trimmed, `Convertido '${m[1]}' de ${m[2]} para ${m[3]}`);
        }
        if ((m = trimmed.match(/^Linked\s+(\d+)x\s+'(.+?)'\s+to\s+kit\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Vinculado ${m[1]}x '${m[2]}' ao kit '${m[3]}'`);
        }
        if ((m = trimmed.match(/^Removed\s+'(.+?)'\s+from\s+kit\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Removido '${m[1]}' do kit '${m[2]}'`);
        }
        if ((m = trimmed.match(/^Set\s+'(.+?)'\s+to\s+(\d+)\s+per\s+kit\s+in\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Definido '${m[1]}' como ${m[2]} por kit em '${m[3]}'`);
        }
        if ((m = trimmed.match(/^Reconciled\s+consumables\s+for\s+kit\s+'(.+?)'\s+\(checked\s+out\s+by\s+'(.+?)'\)$/i))) {
            return text.replace(trimmed, `Consumíveis reconciliados para o kit '${m[1]}' (retirado por '${m[2]}')`);
        }
        if ((m = trimmed.match(/^Sent\s+(\d+)x\s+'(.+?)'\s+out\s+for\s+maintenance\s+to\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Enviado ${m[1]}x '${m[2]}' para manutenção em '${m[3]}'`);
        }
        if ((m = trimmed.match(/^Sent\s+(\d+)x\s+'(.+?)'\s+out\s+for\s+maintenance$/i))) {
            return text.replace(trimmed, `Enviado ${m[1]}x '${m[2]}' para manutenção`);
        }
        if ((m = trimmed.match(/^Returned\s+(\d+)x\s+'(.+?)'\s+from\s+maintenance$/i))) {
            return text.replace(trimmed, `Retornado ${m[1]}x '${m[2]}' da manutenção`);
        }
        if ((m = trimmed.match(/^Updated\s+maintenance\s+schedule\s+for\s+'(.+?)'$/i))) {
            return text.replace(trimmed, `Cronograma de manutenção atualizado para '${m[1]}'`);
        }

        // Numbers & Counts
        if ((m = trimmed.match(/^(\d+)\s+clients?$/i))) {
            return text.replace(trimmed, `${m[1]} cliente${m[1] === '1' ? '' : 's'}`);
        }
        if ((m = trimmed.match(/^(\d+)\s+items?$/i))) {
            return text.replace(trimmed, `${m[1]} ite${m[1] === '1' ? 'm' : 'ns'}`);
        }
        if ((m = trimmed.match(/^Total:\s+(\d+)\s+items?$/i))) {
            return text.replace(trimmed, `Total: ${m[1]} ite${m[1] === '1' ? 'm' : 'ns'}`);
        }
        if ((m = trimmed.match(/^Showing\s+(\d+)\s+of\s+(\d+)\s+items$/i))) {
            return text.replace(trimmed, `Exibindo ${m[1]} de ${m[2]} itens`);
        }
        if ((m = trimmed.match(/^Showing\s+(\d+)\s+result\(s\)$/i))) {
            return text.replace(trimmed, `Exibindo ${m[1]} resultado(s)`);
        }
        if ((m = trimmed.match(/^(\d+)\s+items?\s+currently\s+checked\s+out\s+across\s+(\d+)\s+records?\.?$/i))) {
            return text.replace(trimmed, `${m[1]} ite${m[1] === '1' ? 'm' : 'ns'} retirados no momento em ${m[2]} registro${m[2] === '1' ? '' : 's'}.`);
        }
        if ((m = trimmed.match(/^(\d+)\s+items?\s+marked\s+as\s+lost\.?$/i))) {
            return text.replace(trimmed, `${m[1]} ite${m[1] === '1' ? 'm' : 'ns'} marcado${m[1] === '1' ? '' : 's'} como perdido${m[1] === '1' ? '' : 's'}.`);
        }
        if ((m = trimmed.match(/^(\d+)\s+days?$/i))) {
            return text.replace(trimmed, `${m[1]} dia${m[1] === '1' ? '' : 's'}`);
        }
        if ((m = trimmed.match(/^Expiring\s+within\s+(\d+)\s+days\s+\((\d+)\)$/i))) {
            return text.replace(trimmed, `Vencendo em até ${m[1]} dias (${m[2]})`);
        }
        if ((m = trimmed.match(/^Expired\s+\((\d+)\)$/i))) {
            return text.replace(trimmed, `Vencidos (${m[1]})`);
        }
        if ((m = trimmed.match(/^(\d+)\s+kits?\s+awaiting\s+reconciliation$/i))) {
            return text.replace(trimmed, `${m[1]} kit${m[1] === '1' ? '' : 's'} aguardando reconciliação`);
        }
        if ((m = trimmed.match(/^(\d+)\s+incomplete\s+kits?$/i))) {
            return text.replace(trimmed, `${m[1]} kit${m[1] === '1' ? '' : 's'} incompleto${m[1] === '1' ? '' : 's'}`);
        }
        if ((m = trimmed.match(/^Delete\s+(.+)$/i))) {
            return text.replace(trimmed, `Excluir ${m[1]}`);
        }
        if ((m = trimmed.match(/^Exp\s+(.+)$/i))) {
            return text.replace(trimmed, `Vence ${m[1]}`);
        }
        if ((m = trimmed.match(/^(\d+)\s*–\s*(\d+)\s+of\s+(\d+)$/i))) {
            return text.replace(trimmed, `${m[1]}–${m[2]} de ${m[3]}`);
        }
        if ((m = trimmed.match(/^Showing\s+(\d+)\s*–\s*(\d+)\s+of\s+(\d+)$/i))) {
            return text.replace(trimmed, `Exibindo ${m[1]}–${m[2]} de ${m[3]}`);
        }
        if ((m = trimmed.match(/^Page\s+(\d+)\s+of\s+(\d+)$/i))) {
            return text.replace(trimmed, `Pág. ${m[1]} de ${m[2]}`);
        }
        if ((m = trimmed.match(/^Checked\s+out\s+to\s+(.+?)\.?$/i))) {
            return text.replace(trimmed, `Retirado para ${m[1]}.`);
        }
        if ((m = trimmed.match(/^Consumed\s+(\d+)\s+unit\(s\)\.?$/i))) {
            return text.replace(trimmed, `Consumido ${m[1]} unidade(s).`);
        }
        if ((m = trimmed.match(/^Restocked\s+(\d+)\s+unit\(s\)\.?$/i))) {
            return text.replace(trimmed, `Reposto ${m[1]} unidade(s).`);
        }
        if ((m = trimmed.match(/^Kit\s+checked\s+out\s+to\s+(.+?)\.?$/i))) {
            return text.replace(trimmed, `Kit retirado para ${m[1]}.`);
        }
        return null;
    }

    function translateNode(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            const parent = node.parentNode;
            if (parent && (parent.tagName === 'SCRIPT' || parent.tagName === 'STYLE' || parent.tagName === 'CODE' || parent.tagName === 'PRE')) {
                return;
            }
            const replaced = translateText(node.nodeValue);
            if (replaced !== null && replaced !== node.nodeValue) {
                node.nodeValue = replaced;
            }
        } else if (node.nodeType === Node.ELEMENT_NODE) {
            for (const attr of attrList) {
                if (node.hasAttribute(attr)) {
                    const val = node.getAttribute(attr);
                    const replaced = translateText(val);
                    if (replaced !== null && replaced !== val) {
                        node.setAttribute(attr, replaced);
                    }
                }
            }
            if (node.tagName === 'INPUT' && (node.type === 'button' || node.type === 'submit')) {
                const replaced = translateText(node.value);
                if (replaced !== null && replaced !== node.value) {
                    node.value = replaced;
                }
            }
            for (let child = node.firstChild; child; child = child.nextSibling) {
                translateNode(child);
            }
        }
    }

    function runTranslation() {
        translateNode(document.body);
        if (document.title) {
            const parts = document.title.split(' - ');
            if (parts.length > 0 && exactMap[parts[0].trim()]) {
                parts[0] = exactMap[parts[0].trim()];
                document.title = parts.join(' - ');
            }
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', runTranslation);
    } else {
        runTranslation();
    }

    const observer = new MutationObserver(function (mutations) {
        for (const m of mutations) {
            if (m.type === 'childList') {
                m.addedNodes.forEach(translateNode);
            } else if (m.type === 'characterData') {
                translateNode(m.target);
            }
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            characterData: true
        });
    });
})();
