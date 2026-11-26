/*
 * Genre Collections - home screen rows.
 *
 * Adds rows to the Jellyfin web home screen (sagas, series with new episodes, genres,
 * one row per genre). Rows are loaded while scrolling and posters only when they come
 * near the screen, so a small server or NAS is never asked for hundreds of images at once. It only reuses the web client's own markup
 * and CSS classes (verticalSection, emby-scroller, card, ...) and ships no CSS, so
 * the active theme (e.g. Abyss) styles the rows exactly like the native ones.
 */
(function () {
    'use strict';

    if (window.__genreCollectionsLoaded) {
        return;
    }
    window.__genreCollectionsLoaded = true;

    var CONTAINER_CLASS = 'genreCollectionsSections';
    var REFRESH_MS = 2 * 60 * 1000;
    var lastRender = 0;
    var rendering = false;

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function getApiClient() {
        return window.ApiClient || null;
    }

    function detailsUrl(item, serverId) {
        return '#/details?id=' + encodeURIComponent(item.Id) + '&serverId=' + encodeURIComponent(serverId);
    }

    function imageUrl(apiClient, item) {
        var tag = item.ImageTags && item.ImageTags.Primary;
        if (!tag) {
            return null;
        }
        return apiClient.getScaledImageUrl(item.Id, {
            type: 'Primary',
            fillWidth: 300,
            quality: 90,
            tag: tag
        });
    }

    function cardHtml(apiClient, item) {
        var serverId = apiClient.serverId();
        var url = detailsUrl(item, serverId);
        var img = imageUrl(apiClient, item);
        var name = escapeHtml(item.Name);
        var year = item.ProductionYear ? String(item.ProductionYear) : '';
        var played = item.UserData && item.UserData.Played;

        var html = '<div class="card overflowPortraitCard card-hoverable card-withuserdata"'
            + ' data-id="' + escapeHtml(item.Id) + '" data-serverid="' + escapeHtml(serverId) + '"'
            + ' data-type="' + escapeHtml(item.Type) + '" data-isfolder="' + (item.IsFolder ? 'true' : 'false') + '"'
            + ' data-action="link">';
        html += '<div class="cardBox cardBox-bottompadded">';
        html += '<div class="cardScalable">';
        html += '<div class="cardPadder cardPadder-overflowPortrait"></div>';
        html += '<a href="' + url + '" data-action="link" class="cardImageContainer coveredImage cardContent itemAction"'
            + ' aria-label="' + name + '"'
            + (img ? ' data-src="' + escapeHtml(img) + '"' : '') + '>';
        if (!img) {
            html += '<div class="cardText cardDefaultText">' + name + '</div>';
        }
        if (played) {
            html += '<div class="cardIndicators"><div class="playedIndicator indicator">'
                + '<span class="material-icons indicatorIcon check" aria-hidden="true"></span></div></div>';
        }
        html += '</a>';
        html += '</div>';
        html += '<div class="cardFooter">';
        html += '<div class="cardText cardTextCentered cardText-first"><bdi>' + name + '</bdi></div>';
        html += '<div class="cardText cardTextCentered cardText-secondary"><bdi>' + (year || '&nbsp;') + '</bdi></div>';
        html += '</div>';
        html += '</div></div>';
        return html;
    }

    function sectionHtml(apiClient, title, link, items) {
        var html = '<div class="verticalSection genreCollectionsSection">';
        html += '<div class="sectionTitleContainer sectionTitleContainer-cards padded-left">';
        if (link) {
            html += '<a is="emby-linkbutton" href="' + link + '" class="more button-flat button-flat-mini sectionTitleTextButton">';
            html += '<h2 class="sectionTitle sectionTitle-cards">' + escapeHtml(title) + '</h2>';
            html += '<span class="material-icons chevron_right" aria-hidden="true"></span>';
            html += '</a>';
        } else {
            html += '<h2 class="sectionTitle sectionTitle-cards">' + escapeHtml(title) + '</h2>';
        }
        html += '</div>';
        html += '<div is="emby-scroller" class="padded-top-focusscale padded-bottom-focusscale" data-centerfocus="true">';
        html += '<div is="emby-itemscontainer" class="itemsContainer scrollSlider focuscontainer-x">';
        for (var i = 0; i < items.length; i++) {
            html += cardHtml(apiClient, items[i]);
        }
        html += '</div></div></div>';
        return html;
    }

    // ---------- Network: at most MAX_PARALLEL item queries at a time ----------

    var MAX_PARALLEL = 4;
    var active = 0;
    var waiting = [];

    function limited(fn) {
        return new Promise(function (resolve, reject) {
            waiting.push(function () {
                active++;
                Promise.resolve().then(fn).then(resolve, reject).then(function () {
                    active--;
                    next();
                });
            });
            next();
        });
    }

    function next() {
        while (active < MAX_PARALLEL && waiting.length) {
            waiting.shift()();
        }
    }

    // ---------- Posters: loaded only when they come near the screen ----------

    var imageObserver = null;

    function showImage(el) {
        var src = el && el.getAttribute('data-src');
        if (src) {
            el.style.backgroundImage = "url('" + src.replace(/'/g, '%27') + "')";
            el.removeAttribute('data-src');
        }
    }

    function observeImages(root) {
        var images = root.querySelectorAll('[data-src]');
        if (!('IntersectionObserver' in window)) {
            images.forEach(showImage);
            return;
        }
        if (!imageObserver) {
            imageObserver = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (!entry.isIntersecting) {
                        return;
                    }
                    imageObserver.unobserve(entry.target);
                    showImage(entry.target);
                    // Also prepare the next few posters of the row, so scrolling sideways does not show empty cards.
                    var card = entry.target.closest('.card');
                    for (var n = 0; card && n < 4; n++) {
                        card = card.nextElementSibling;
                        if (card) {
                            showImage(card.querySelector('[data-src]'));
                        }
                    }
                });
            }, { rootMargin: '300px' });
        }
        images.forEach(function (el) { imageObserver.observe(el); });
    }

    function fetchItems(apiClient, query) {
        var base = {
            Recursive: true,
            Fields: 'PrimaryImageAspectRatio,ProductionYear',
            ImageTypeLimit: 1,
            EnableImageTypes: 'Primary',
            EnableTotalRecordCount: false,
            // Never replace movies by the collections they belong to ("group movies into collections" setting)
            CollapseBoxSetItems: false,
            // Only titles really present on the server (no placeholders / missing items)
            IsMissing: false,
            LocationTypes: 'FileSystem,Remote'
        };
        for (var key in query) {
            if (Object.prototype.hasOwnProperty.call(query, key)) {
                if (query[key] === null) {
                    delete base[key];
                } else {
                    base[key] = query[key];
                }
            }
        }
        return limited(function () { return apiClient.getItems(apiClient.getCurrentUserId(), base); })
            .then(function (result) { return (result && result.Items) || []; })
            .catch(function (err) {
                console.warn('[GenreCollections] item query failed', err);
                return [];
            });
    }

    function buildSections(apiClient, config) {
        var limit = config.ItemsPerSection || 20;
        var serverId = apiClient.serverId();
        var jobs = [];

        // First row: movie sagas (Harry Potter, Hunger Games, ...), one poster per saga.
        if (config.Sagas && config.Sagas.length) {
            jobs.push(function () { return fetchItems(apiClient, {
                Ids: config.Sagas.map(function (c) { return c.Id; }).join(','),
                Recursive: false,
                SortBy: 'SortName',
                SortOrder: 'Ascending'
            }).then(function (items) {
                return { title: config.Labels.Sagas, link: null, items: items };
            }); });
        }

        if (config.ShowLatestMovies) {
            jobs.push(function () { return fetchItems(apiClient, {
                IncludeItemTypes: 'Movie',
                SortBy: 'DateCreated,SortName',
                SortOrder: 'Descending',
                Limit: limit
            }).then(function (items) {
                return { title: config.Labels.LatestMovies, link: null, items: items };
            }); });
        }

        if (config.ShowLatestSeries) {
            jobs.push(function () { return fetchItems(apiClient, {
                IncludeItemTypes: 'Series',
                SortBy: 'DateLastContentAdded,SortName',
                SortOrder: 'Descending',
                Limit: limit
            }).then(function (items) {
                return { title: config.Labels.LatestSeries, link: null, items: items };
            }); });
        }

        // Whole library, A to Z; the title opens the full list.
        if (config.ShowAllMovies) {
            jobs.push(function () { return fetchItems(apiClient, {
                IncludeItemTypes: 'Movie',
                SortBy: 'SortName',
                SortOrder: 'Ascending',
                Limit: limit
            }).then(function (items) {
                return { title: config.Labels.AllMovies, link: '#/list?type=Movie&serverId=' + encodeURIComponent(serverId), items: items };
            }); });
        }

        if (config.ShowAllSeries) {
            jobs.push(function () { return fetchItems(apiClient, {
                IncludeItemTypes: 'Series',
                SortBy: 'SortName',
                SortOrder: 'Ascending',
                Limit: limit
            }).then(function (items) {
                return { title: config.Labels.AllSeries, link: '#/list?type=Series&serverId=' + encodeURIComponent(serverId), items: items };
            }); });
        }

        if (config.ShowCollectionsRow && config.Collections.length) {
            var ids = config.Collections.map(function (c) { return c.Id; }).join(',');
            // Native Jellyfin genres: each card opens the genre page (its movies and series).
            jobs.push(function () { return fetchItems(apiClient, {
                Ids: ids,
                Recursive: false,
                IsMissing: null,
                LocationTypes: null,
                SortBy: 'SortName',
                SortOrder: 'Ascending'
            }).then(function (items) {
                return { title: config.Labels.Collections, link: null, items: items };
            }); });
        }

        if (config.ShowGenreRows) {
            config.GenreRows.forEach(function (genre) {
                if (!genre.ItemIds || !genre.ItemIds.length) {
                    return;
                }
                // Plain movies and series (never episodes or collections): every card opens the normal
                // movie / series page, with its seasons and episodes.
                var query = {
                    Ids: genre.ItemIds.join(','),
                    Recursive: false,
                    IncludeItemTypes: 'Movie,Series',
                    SortBy: config.GenreRowSort === 'Random' ? 'Random' : (config.GenreRowSort + ',SortName'),
                    SortOrder: 'Descending',
                    Limit: limit
                };
                if (config.HideWatchedInGenreRows) {
                    query.IsPlayed = false;
                }
                jobs.push(function () { return fetchItems(apiClient, query).then(function (items) {
                    return {
                        title: genre.Name,
                        link: genre.GenreId ? '#/details?id=' + encodeURIComponent(genre.GenreId) + '&serverId=' + encodeURIComponent(serverId) : null,
                        // Too few titles left (e.g. all watched): no row.
                        items: items.length >= (config.MinimumTitlesPerGenreRow || 0) ? items : []
                    };
                }); });
            });
        }

        return jobs;
    }

    function getHomeTab() {
        var page = document.querySelector('#indexPage:not(.hide)');
        return page ? page.querySelector('#homeTab') : null;
    }

    function ensureContainer(homeTab, position) {
        var container = homeTab.querySelector('.' + CONTAINER_CLASS);
        var sections = homeTab.querySelector('.sections');
        if (!container) {
            container = document.createElement('div');
            container.className = CONTAINER_CLASS;
        }
        if (sections) {
            if (position === 'Top') {
                if (container.nextSibling !== sections) {
                    homeTab.insertBefore(container, sections);
                }
            } else if (sections.nextSibling !== container) {
                sections.parentNode.insertBefore(container, sections.nextSibling);
            }
        } else if (!container.parentNode) {
            homeTab.appendChild(container);
        }
        return container;
    }

    // ---------- Rendering: first rows at once, the others while scrolling ----------

    var FIRST_ROWS = 3;
    var ROWS_PER_STEP = 2;
    var state = null;

    function idsOf(items) {
        return items.map(function (i) { return i.Id; }).join(',');
    }

    // Fills one row slot; keeps the DOM as is when the row did not change (no jump on refresh).
    function loadSlot(apiClient, slot, load) {
        slot.loading = true;
        return load().then(function (s) {
            var signature = s.title + '|' + (s.link || '') + '|' + idsOf(s.items);
            if (slot.getAttribute('data-signature') !== signature) {
                slot.innerHTML = s.items.length ? sectionHtml(apiClient, s.title, s.link, s.items) : '';
                slot.setAttribute('data-signature', signature);
                observeImages(slot);
            }
            slot.loaded = true;
        }).catch(function (err) {
            console.warn('[GenreCollections] row failed', err);
        }).then(function () {
            slot.loading = false;
        });
    }

    function loadMore() {
        if (!state) {
            return Promise.resolve();
        }
        var current = state;
        var pending = [];
        for (var i = 0; i < current.slots.length && pending.length < ROWS_PER_STEP; i++) {
            var slot = current.slots[i];
            if (!slot.loaded && !slot.loading) {
                pending.push(loadSlot(current.apiClient, slot, current.loads[i]));
            }
        }
        if (!pending.length) {
            if (current.observer) {
                current.observer.disconnect();
            }
            return Promise.resolve();
        }
        return Promise.all(pending).then(function () {
            // Re-observing fires again at once if the end of the rows is still near the screen.
            if (state === current && current.observer) {
                current.observer.unobserve(current.sentinel);
                current.observer.observe(current.sentinel);
            }
        });
    }

    function renderInto(container, apiClient, loads, refresh) {
        if (state && state.observer) {
            state.observer.disconnect();
        }

        // One slot per row, reused when refreshing so that unchanged rows stay in place.
        var slots = Array.prototype.slice.call(container.querySelectorAll(':scope > .genreCollectionsSlot'));
        while (slots.length > loads.length) {
            slots.pop().remove();
        }
        var sentinel = container.querySelector(':scope > .genreCollectionsSentinel');
        if (!sentinel) {
            sentinel = document.createElement('div');
            sentinel.className = 'genreCollectionsSentinel';
            container.appendChild(sentinel);
        }
        while (slots.length < loads.length) {
            var slot = document.createElement('div');
            slot.className = 'genreCollectionsSlot';
            container.insertBefore(slot, sentinel);
            slots.push(slot);
        }

        state = { apiClient: apiClient, slots: slots, loads: loads, sentinel: sentinel, observer: null };

        var first = [];
        slots.forEach(function (slot, i) {
            if (i < FIRST_ROWS || (refresh && slot.loaded)) {
                slot.loaded = false;
                first.push(loadSlot(apiClient, slot, loads[i]));
            } else if (!refresh) {
                slot.loaded = false;
            }
        });

        if ('IntersectionObserver' in window) {
            var current = state;
            current.observer = new IntersectionObserver(function (entries) {
                if (entries.some(function (e) { return e.isIntersecting; }) && state === current) {
                    loadMore();
                }
            }, { rootMargin: '600px' });
            Promise.all(first).then(function () {
                if (state === current) {
                    current.observer.observe(sentinel);
                }
            });
        } else {
            slots.forEach(function (slot, i) {
                if (i >= FIRST_ROWS) {
                    loadSlot(apiClient, slot, loads[i]);
                }
            });
        }
        return Promise.all(first);
    }

    function render(force) {
        var apiClient = getApiClient();
        var homeTab = getHomeTab();
        if (!apiClient || !homeTab || rendering || !apiClient.getCurrentUserId()) {
            return;
        }
        var existing = homeTab.querySelector('.' + CONTAINER_CLASS);
        if (!force && existing && existing.childElementCount && Date.now() - lastRender < REFRESH_MS) {
            return;
        }

        rendering = true;
        apiClient.getJSON(apiClient.getUrl('GenreCollections/HomeSections'))
            .then(function (config) {
                if (!config || !config.Enabled) {
                    if (existing) {
                        existing.remove();
                    }
                    return null;
                }
                var container = ensureContainer(homeTab, config.Position);
                var refresh = !!container.querySelector('.genreCollectionsSlot');
                lastRender = Date.now();
                return renderInto(container, apiClient, buildSections(apiClient, config), refresh);
            })
            .catch(function (err) {
                console.warn('[GenreCollections] unable to render home sections', err);
            })
            .then(function () {
                rendering = false;
            });
    }

    // The home view fires "viewshow" every time it is displayed.
    document.addEventListener('viewshow', function (e) {
        var target = e.target;
        if (target && target.id === 'indexPage') {
            // Let the native sections render first.
            setTimeout(function () { render(false); }, 300);
        }
    }, true);

    // First load: the page may already be visible when this script runs.
    var tries = 0;
    var timer = setInterval(function () {
        tries++;
        if (getHomeTab() && getApiClient()) {
            render(false);
            clearInterval(timer);
        } else if (tries > 40) {
            clearInterval(timer);
        }
    }, 500);
})();
