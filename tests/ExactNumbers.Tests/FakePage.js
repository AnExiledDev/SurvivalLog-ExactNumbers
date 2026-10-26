// Just enough of Root.html and two game pages for page.js to run in Jint: the root document with its page iframes, the
// vuplex bridge, an object menu drawn the way SelectMenu.html draws it, and a crafting table whose recipe tooltip is
// built the way ToolTable.html builds it. Test code only; nothing here ships.
var window = this;

var intervals = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

// One run of every interval the script set, which is how its poll runs.
function poll() {
  intervals.slice().forEach(function (callback) { callback(); });
}

// requestAnimationFrame on the page windows: callbacks wait here until a test runs the next frame.
var animationFrames = [];

function requestAnimationFrame(callback) {
  animationFrames.push(callback);

  return animationFrames.length;
}

function frame() {
  var due = animationFrames;

  animationFrames = [];
  due.forEach(function (callback) { callback(); });
}

// Every getElementById, querySelector and querySelectorAll on any fake page, so a test can see a poll search nothing.
var searches = 0;

var FakeElement = class {
  constructor(tagName, className) {
    var element = this;

    this.tagName = tagName;
    this.id = '';
    this.parentElement = null;
    this.children = [];
    this.text = '';
    this.offsetWidth = 0;
    this.attrs = {};
    this.classes = className ? className.split(' ') : [];
    this.style = {};
    this.classList = {
      contains: function (name) { return element.classes.indexOf(name) >= 0; },
      add: function () {
        for (var i = 0; i < arguments.length; i++) {
          if (element.classes.indexOf(arguments[i]) < 0) element.classes.push(arguments[i]);
        }
      },
      remove: function () {
        var names = Array.prototype.slice.call(arguments);

        element.classes = element.classes.filter(function (name) { return names.indexOf(name) < 0; });
      }
    };
  }

  get lastElementChild() { return this.children.length ? this.children[this.children.length - 1] : null; }

  // In a browser, setting the text replaces the element's text node, which a watching MutationObserver hears.
  get textContent() { return this.text; }

  set textContent(value) {
    this.text = String(value);
    noteMutation(this, { type: 'childList', addedNodes: [{ nodeType: 3 }], removedNodes: [{ nodeType: 3 }] });
  }

  // In a document: under the body of one, however deep.
  get isConnected() {
    for (var element = this; element; element = element.parentElement) {
      if (element.isBody) return true;
    }

    return false;
  }

  // The title property reflects the attribute, as in a browser.
  get title() { return this.getAttribute('title') || ''; }

  set title(value) { this.setAttribute('title', value); }

  hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name); }

  getAttribute(name) { return this.hasAttribute(name) ? this.attrs[name] : null; }

  setAttribute(name, value) { this.attrs[name] = String(value); }

  removeAttribute(name) { delete this.attrs[name]; }

  appendChild(child) {
    child.parentElement = this;
    this.children.push(child);
    noteMutation(this, { type: 'childList', addedNodes: [child], removedNodes: [] });

    return child;
  }

  removeChild(child) {
    this.children = this.children.filter(function (c) { return c !== child; });
    child.parentElement = null;
    noteMutation(this, { type: 'childList', addedNodes: [], removedNodes: [child] });

    return child;
  }

  // Not a DOM call: what the fake pages use to clear an element before drawing it again.
  empty() {
    this.children.slice().forEach(function (child) { this.removeChild(child); }, this);
  }

  contains(other) {
    for (var element = other; element; element = element.parentElement) {
      if (element === this) return true;
    }

    return false;
  }

  matches(selector) {
    var element = this;

    return selectorSteps(selector).every(function (step) { return matchesStep(element, step); });
  }

  closest(selector) {
    for (var element = this; element; element = element.parentElement) {
      if (element.matches(selector)) return element;
    }

    return null;
  }

  querySelectorAll(selector) {
    searches += 1;

    return matching(this, selector);
  }

  querySelector(selector) {
    searches += 1;

    return matching(this, selector)[0] || null;
  }

  getBoundingClientRect() { return { left: 0, right: 0, top: 0, bottom: 0 }; }
};

// Compound selectors of classes and attributes (".menu-btn[data-index]"); the script uses nothing else.
function selectorSteps(selector) {
  var steps = selector.match(/\.[\w-]+|\[[\w-]+\]/g) || [];

  if (steps.join('') !== selector) throw new Error('the fake page does not know the selector "' + selector + '"');

  return steps;
}

function matching(root, selector) {
  return descendantsOf(root).filter(function (element) { return element.matches(selector); });
}

function matchesStep(element, step) {
  return step.charAt(0) === '.' ? element.classList.contains(step.slice(1)) : element.hasAttribute(step.slice(1, -1));
}

function descendantsOf(root) {
  var found = [];

  root.children.forEach(function walk(child) {
    found.push(child);
    child.children.forEach(walk);
  });

  return found;
}

var FakeDocument = class {
  constructor() {
    this.readyState = 'complete';
    this.body = new FakeElement('body');
    this.body.isBody = true;
    this.listeners = {};
  }

  getElementById(id) {
    searches += 1;

    return descendantsOf(this.body).filter(function (element) { return element.id === id; })[0] || null;
  }

  getElementsByTagName(tagName) {
    return descendantsOf(this.body).filter(function (element) { return element.tagName === tagName; });
  }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }
};

// Hears the fake DOM's own changes under what it observes (always childList and subtree, all the script asks for).
// Records wait until a test delivers them, as a browser delivers them after the change that made them.
var observers = [];

var FakeMutationObserver = class {
  constructor(callback) {
    this.callback = callback;
    this.targets = [];
    this.records = [];
    observers.push(this);
  }

  observe(target) { this.targets.push(target); }

  takeRecords() {
    var taken = this.records;

    this.records = [];

    return taken;
  }
};

function noteMutation(target, record) {
  observers.forEach(function (observer) {
    if (observer.targets.some(function (watched) { return watched.contains(target); })) observer.records.push(record);
  });
}

// Each observer with records gets them all in one call.
function deliverMutations() {
  observers.forEach(function (observer) {
    var records = observer.takeRecords();

    if (records.length) observer.callback(records, observer);
  });
}

// Root.html's own document, which holds the page iframes.
var document = new FakeDocument();

// The vuplex bridge: what the script posts to the plugin, and the game's messages to pages as Root.html hears them.
var posted = [];
var gameListeners = [];

window.vuplex = {
  addEventListener: function (type, listener) {
    if (type === 'message') gameListeners.push(listener);
  },
  postMessage: function (message) { posted.push(message); }
};

// A message for one page, in the form Root.html receives it (Root.html:252-262).
function gameSends(pageId, name, json) {
  var message = ['8', pageId, name, json].join('\u001e');

  gameListeners.slice().forEach(function (listener) { listener({ data: message }); });
}

// A chip per {k: chip class, v: drawn text}, its text in the last child, as both pages draw them.
function drawChips(wrap, chips) {
  chips.forEach(function (chip) {
    var element = wrap.appendChild(new FakeElement('span', 'attr-chip ' + chip.k));

    element.appendChild(new FakeElement('span', 'attr-icon'));
    element.appendChild(new FakeElement('span', 'attr-value')).textContent = chip.v;
  });

  return wrap;
}

function addFrame(id, src, win, doc) {
  var frame = document.body.appendChild(new FakeElement('iframe'));

  frame.id = id;
  frame.src = src;
  frame.contentWindow = win;
  frame.contentDocument = doc;

  return frame;
}

function pageWindow() {
  return { MutationObserver: FakeMutationObserver, requestAnimationFrame: requestAnimationFrame, innerWidth: 1920 };
}

// An object menu as the game shows one: a fresh iframe under the page id replaces the last open's, and SelectMenu.html
// draws a .menu-btn per btnList entry, data-index its place in the list, with a .tip-attrs chip per change in its
// preview. buttons: one list of chips per button.
function openMenu(pageId, buttons) {
  var old = document.getElementById(pageId);
  var doc = new FakeDocument();
  var container = doc.body.appendChild(new FakeElement('div'));

  if (old) old.parentElement.removeChild(old);

  container.id = 'menu-container';
  drawButtons(container.appendChild(new FakeElement('div', 'menu-col maint')), buttons);
  addFrame(pageId, 'https://game.local/UI/SelectMenu/SelectMenu.html', pageWindow(), doc);
}

// The open menu drawing its buttons again in the same container, with the game's marks.
function redrawMenu(pageId, buttons) {
  var column = document.getElementById(pageId).contentDocument.getElementById('menu-container').children[0];

  column.empty();
  drawButtons(column, buttons);
}

function drawButtons(column, buttons) {
  buttons.forEach(function (chips, index) {
    var button = column.appendChild(new FakeElement('div', 'menu-btn'));
    var tooltip = button.appendChild(new FakeElement('div', 'menu-btn-tooltip'));

    button.setAttribute('data-index', index);
    drawChips(tooltip.appendChild(new FakeElement('div', 'tip-attrs')), chips);
  });
}

function menuChip(pageId, button, chip) {
  var doc = document.getElementById(pageId).contentDocument;

  return doc.getElementById('menu-container').querySelectorAll('.tip-attrs')[button].children[chip].lastElementChild.textContent;
}

// The crafting table: buildAttrChips(preview) draws a .tip-attrs for a preview (ToolTable.html:3445),
// buildRecipeProduct(recipe) makes the product object a tooltip is shown with (3420), and
// showRecipeTooltip(event, materials, craftLockLine, product) refills the shared #recipe-tooltip with the product's
// chips and shows it (3491, chips at 3540). All are page-wide, so the page's own calls go through the window. draws:
// preview -> the chips the game draws for it. builds counts the recipe tooltips built.
function openToolTable(draws) {
  var doc = new FakeDocument();
  var win = pageWindow();
  var tooltip = doc.body.appendChild(new FakeElement('div'));

  tooltip.id = 'recipe-tooltip';
  tooltip.style.display = 'none';
  win.builds = 0;
  win.buildAttrChips = function (preview) {
    return drawChips(new FakeElement('div', 'tip-attrs'), draws[preview] || []);
  };
  win.products = {};
  win.buildRecipeProduct = function (recipe) {
    return { name: recipe.name || '', attrDelta: recipe.productAttrDelta || '' };
  };
  // more: further chip rows, which the real page does not draw, to hold the script to waiting for every number in a
  // tooltip before building it again.
  win.showRecipeTooltip = function (event, materials, craftLockLine, product) {
    win.builds += 1;
    tooltip.empty();

    if (product && product.attrDelta) tooltip.appendChild(win.buildAttrChips(product.attrDelta));

    ((product && product.more) || []).forEach(function (preview) { tooltip.appendChild(win.buildAttrChips(preview)); });
    tooltip.style.display = 'block';
  };
  // The page reuses the same tooltip element for a material's tooltip (ToolTable.html:3624).
  win.showMaterialTooltip = function () {
    tooltip.empty();
    tooltip.appendChild(new FakeElement('div', 'material-row'));
    tooltip.style.display = 'block';
  };
  win.hideTooltip = function () { tooltip.style.display = 'none'; };
  addFrame('ToolTable', 'https://game.local/UI/ToolTable/ToolTable.html', win, doc);
}

function toolTable() {
  return document.getElementById('ToolTable').contentWindow;
}

// The recipe list as the page draws it from a recipe message: each row keeps the product built from its recipe
// (buildRecipeItem, ToolTable.html:3271). recipes: [{recipeKey, productAttrDelta}].
function drawRecipes(recipes) {
  var win = toolTable();

  win.products = {};
  recipes.forEach(function (recipe) { win.products[recipe.recipeKey] = win.buildRecipeProduct(recipe); });
}

// Hovering a recipe row of the list.
function hoverProduct(recipeKey) {
  var win = toolTable();

  win.showRecipeTooltip({ clientX: 40, clientY: 50 }, [], '', win.products[recipeKey]);
}

// A tooltip for a product no recipe row was drawn for: the first preview as its chips, any others as further rows.
function hoverRecipe(previews) {
  toolTable().showRecipeTooltip({ clientX: 40, clientY: 50 }, [], '', { attrDelta: previews[0], more: previews.slice(1) });
}

function recipeChip(wrap, chip) {
  var tooltip = document.getElementById('ToolTable').contentDocument.getElementById('recipe-tooltip');

  return tooltip.children[wrap].children[chip].lastElementChild.textContent;
}
